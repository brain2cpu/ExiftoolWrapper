using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Brain2CPU.ExifTool
{
    public struct ExifToolResponse
    {
        public bool IsSuccess { get; }
        public string Result { get; }

        public ExifToolResponse(string r)
        {
            IsSuccess = r.ToLowerInvariant().Contains(ExifToolWrapper.SuccessMessage);
            Result = r;
        }

        public ExifToolResponse(bool b, string r)
        {
            IsSuccess = b;
            Result = r;
        }

        //to use ExifToolResponse directly in if (discarding response)
        public static implicit operator bool(ExifToolResponse r) => r.IsSuccess;
    }

    public sealed class ExifToolWrapper : IDisposable
    {
        public string ExifToolPath    { get; }
        public string ExifToolVersion { get; private set; }

        private  const string ExeName         = "exiftool(-k).exe";
        private  const string Arguments       = "-fast  -m -q -q -stay_open True -@ - -common_args -d \"%Y.%m.%d %H:%M:%S\" -c \"%d %d %.6f\" -t -charset utf8";   //-g for groups
        private  const string ArgumentsFaster = "-fast2 -m -q -q -stay_open True -@ - -common_args -d \"%Y.%m.%d %H:%M:%S\" -c \"%d %d %.6f\" -t -charset utf8";
        private  const string ExitMessage     = "-- press RETURN --";
        internal const string SuccessMessage  = "1 image files updated";

        //-fast2 also causes exiftool to avoid extracting any EXIF MakerNote information

        public double SecondsToWaitForError { get; set; } = 1;
        public double SecondsToWaitForStop  { get; set; } = 5;

        public enum ExeStatus { Stopped, Starting, Ready, Stopping }
        public ExeStatus Status { get; private set; }

        //ViaFile: for every command an argument file is created, it works for files with accented characters but it is slower
        public enum CommunicationMethod { Auto, Direct, ViaFile }
        public CommunicationMethod Method { get; set; } = CommunicationMethod.Auto;

        public bool Resurrect { get; set; } = true;

        private bool _stopRequested = false;

        private int _cmdCnt = 0;
        private readonly StringBuilder _output = new StringBuilder();
        private readonly StringBuilder _error  = new StringBuilder();

        private readonly ProcessStartInfo _psi;
        private Process _proc = null;

        private readonly ManualResetEvent _waitHandle = new ManualResetEvent(true);
        private readonly ManualResetEvent _waitForErrorHandle = new ManualResetEvent(true);

        public ExifToolWrapper(string path = null, bool faster = false)
        {
            if(string.IsNullOrEmpty(path))
            {
                if(File.Exists(ExeName)) //in current directory
                    ExifToolPath = Path.GetFullPath(ExeName);
                else
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                        ExifToolPath = Path.Combine(dir, ExeName);
                    }
                    catch(Exception xcp)
                    {
                        Debug.WriteLine(xcp.ToString());
                        ExifToolPath = ExeName;
                    }
                }
            }
            else
                ExifToolPath = path;

            if(!File.Exists(ExifToolPath))
                throw new ExifToolException($"{ExeName} not found");

            _psi = new ProcessStartInfo
            {
                FileName = ExifToolPath,
                Arguments = faster ? ArgumentsFaster : Arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            Status = ExeStatus.Stopped;
        }

        private void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if(string.IsNullOrEmpty(e?.Data))
                return;

            if(Status == ExeStatus.Starting)
            {
                ExifToolVersion = e.Data;
                _waitHandle.Set();

                return;
            }

            if(string.Equals(e.Data, $"{{ready{_cmdCnt}}}", StringComparison.OrdinalIgnoreCase))
            {
                _waitHandle.Set();

                return;
            }

            _output.AppendLine(e.Data);
        }

        //the error message has no 'ready' or other terminator so we must assume it has a single line (or it is received fast enough)
        private void ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if(string.IsNullOrEmpty(e?.Data))
                return;

            if(string.Equals(e.Data, ExitMessage, StringComparison.OrdinalIgnoreCase))
            {
                _proc?.StandardInput.WriteLine();

                return;
            }

            _error.AppendLine(e.Data);
            _waitForErrorHandle.Set();
        }

        public void Start()
        {
            _stopRequested = false;

            if(Status != ExeStatus.Stopped)
                throw new ExifToolException("Process is not stopped");

            Status = ExeStatus.Starting;

            _proc = new Process { StartInfo = _psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += OutputDataReceived;
            _proc.ErrorDataReceived += ErrorDataReceived;
            _proc.Exited += ProcExited;
            _proc.Start();

            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
            _proc.StandardInput.AutoFlush = true;

            _waitHandle.Reset();
            _proc.StandardInput.Write("-ver\n-execute0000\n");
            _waitHandle.WaitOne();

            Status = ExeStatus.Ready;
        }

        //detect if process was killed
        private void ProcExited(object sender, EventArgs e)
        {
            if(_proc != null)
            {
                _proc.Dispose();
                _proc = null;
            }

            Status = ExeStatus.Stopped;

            _waitHandle.Set();

            if(!_stopRequested && Resurrect)
                Start();
        }

        public void Stop()
        {
            _stopRequested = true;

            if(Status != ExeStatus.Ready)
                throw new ExifToolException("Process must be ready");

            Status = ExeStatus.Stopping;

            _waitHandle.Reset();
            _proc.StandardInput.Write("-stay_open\nFalse\n");
            
            if(!_waitHandle.WaitOne(TimeSpan.FromSeconds(SecondsToWaitForStop)))
            {
                if(_proc != null)
                {
                    try
                    {
                        _proc.Kill();
                        _proc.WaitForExit((int)(1000 * SecondsToWaitForStop / 2));

                        // after WaitForExit proc turns null
                        _proc?.Dispose();
                    }
                    catch(Exception xcp)
                    {
                        Debug.WriteLine(xcp.ToString());
                    }

                    _proc = null;
                }

                Status = ExeStatus.Stopped;
            }
        }

        private readonly object _lockObj = new object();

        private void DirectSend(string cmd, params object[] args)
        {
            _proc.StandardInput.Write("{0}\n-execute{1}\n", args.Length == 0 ? cmd : string.Format(cmd, args), _cmdCnt);
            _waitHandle.WaitOne();
        }
        
        //http://u88.n24.queensu.ca/exiftool/forum/index.php?topic=8382.0
        private void SendViaFile(string cmd, params object[] args)
        {
            var argFile = Path.GetTempFileName();
            try
            {
                var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                File.WriteAllText(argFile, args.Length == 0 ? cmd : string.Format(cmd, args), utf8NoBom);

                _proc.StandardInput.Write("-@\n{0}\n-execute{1}\n", argFile, _cmdCnt);
                _waitHandle.WaitOne();
            }
            finally
            {
                File.Delete(argFile);
            }
        }

        public ExifToolResponse SendCommand(string cmd, params object[] args) => SendCommand(Method, cmd, args);
        
        private ExifToolResponse SendCommand(CommunicationMethod method, string cmd, params object[] args)
        {
            if(Status != ExeStatus.Ready)
                throw new ExifToolException("Process must be ready");

            ExifToolResponse resp;
            lock(_lockObj)
            {
                _waitHandle.Reset();
                _waitForErrorHandle.Reset();

                if(method == CommunicationMethod.ViaFile)
                    SendViaFile(cmd, args);
                else
                    DirectSend(cmd, args);

                //if no output then probably there is an error, so wait at most SecondsToWaitForError for the error message to arrive 
                if(_output.Length == 0)
                {
                    _waitForErrorHandle.WaitOne(TimeSpan.FromSeconds(SecondsToWaitForError));
                    resp = new ExifToolResponse(false, _error.ToString());
                    _error.Clear();
                }
                else
                {
                    resp = new ExifToolResponse(true, _output.ToString());
                    _output.Clear();
                }

                _cmdCnt++;
            }

            if(!resp.IsSuccess && method == CommunicationMethod.Auto)
            {
                string err = resp.Result.ToLowerInvariant();

                if(err.Contains("file not found") || err.Contains("invalid filename encoding"))
                     return SendCommand(CommunicationMethod.ViaFile, cmd, args);
            }

            return resp;
        }

        public ExifToolResponse SetExifInto(string path, string key, string val, bool overwriteOriginal = true) =>
            SetExifInto(path, new Dictionary<string, string> {[key] = val}, overwriteOriginal);

        public ExifToolResponse SetExifInto(string path, Dictionary<string, string> data, bool overwriteOriginal = true)
        {
            if(!File.Exists(path))
                return new ExifToolResponse(false, $"'{path}' not found");

            var cmd = new StringBuilder();
            foreach(KeyValuePair<string, string> kv in data)
            {
                cmd.AppendFormat("-{0}={1}\n", kv.Key, kv.Value);
            }
            
            if(overwriteOriginal)
                cmd.Append("-overwrite_original\n");
            
            cmd.Append(path);
            var cmdRes = SendCommand(cmd.ToString());

            //if failed return as it is, if it's success must check the response
            return cmdRes ? new ExifToolResponse(cmdRes.Result) : cmdRes;
        }

        public Dictionary<string, string> FetchExifFrom(string path, IEnumerable<string> tagsToKeep = null, bool keepKeysWithEmptyValues = true)
        {
            var res = new Dictionary<string, string>();

            if(!File.Exists(path))
                return res;

            var tagsTable = tagsToKeep?.ToDictionary(x => x, x => 1);
            bool filter = tagsTable != null && tagsTable.Count > 0;
            var cmdRes = SendCommand(path);
            if(!cmdRes)
                return res;

            foreach(string s in cmdRes.Result.Split(new [] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = s.Split('\t');
                //Debug.Assert(kv.Length == 2, $"Can not parse line :'{s}'");

                if(kv.Length != 2 || (!keepKeysWithEmptyValues && string.IsNullOrEmpty(kv[1])))
                    continue;

                if(filter && !tagsTable.ContainsKey(kv[0]))
                    continue;
                    
                res[kv[0]] = kv[1];
            }

            return res;
        }

        public List<string> FetchExifToListFrom(string path, IEnumerable<string> tagsToKeep = null, bool keepKeysWithEmptyValues = true, string separator = ": ")
        {
            var res = new List<string>();

            if(!File.Exists(path))
                return res;

            var tagsTable = tagsToKeep?.ToDictionary(x => x, x => 1);
            bool filter = tagsTable?.Count > 0;
            var cmdRes = SendCommand(path);
            if(!cmdRes)
                return res;

            foreach(string s in cmdRes.Result.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = s.Split('\t');
                Debug.Assert(kv.Length == 2, $"Can not parse line :'{s}'");

                if(kv.Length != 2 || (!keepKeysWithEmptyValues && string.IsNullOrEmpty(kv[1])))
                    continue;

                if(filter && !tagsTable.ContainsKey(kv[0]))
                    continue;

                res.Add($"{kv[0]}{separator}{kv[1]}");
            }

            return res;
        }

        public ExifToolResponse CloneExif(string source, string dest, bool backup = false)
        {
            if(!File.Exists(source) || !File.Exists(dest))
                return new ExifToolResponse(false, $"'{source}' or '{dest}' not found");

            var cmdRes = SendCommand("{0}-tagsFromFile\n{1}\n{2}", backup ? "" : "-overwrite_original\n", source, dest);

            return cmdRes ? new ExifToolResponse(cmdRes.Result) : cmdRes;
        }

        public ExifToolResponse ClearExif(string path, bool backup = false)
        {
            if(!File.Exists(path))
                return new ExifToolResponse(false, $"'{path}' not found");

            var cmdRes = SendCommand("{0}-all=\n{1}", backup ? "" : "-overwrite_original\n", path);

            return cmdRes ? new ExifToolResponse(cmdRes.Result) : cmdRes;
        }

        public DateTime? GetCreationTime(string path)
        {
            if(!File.Exists(path))
                return null;

            var cmdRes = SendCommand("-DateTimeOriginal\n-s3\n{0}", path);
            if(!cmdRes)
                return null;

            if(DateTime.TryParseExact(cmdRes.Result,
                "yyyy.MM.dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime dt))
                return dt;

            return null;
        }

        public int GetOrientation(string path)
        {
            if(!File.Exists(path))
                return 1;

            var cmdRes = SendCommand("-Orientation\n-n\n-s3\n{0}", path);
            if(!cmdRes)
                return 1;

            if(int.TryParse(cmdRes.Result.Trim('\t', '\r', '\n'), out int o))
                return o;

            return 1;
        }

        public int GetOrientationDeg(string path) => OrientationPos2Deg(GetOrientation(path));

        public ExifToolResponse SetOrientation(string path, int ori, bool overwriteOriginal = true)
        {
            if(!File.Exists(path))
                return new ExifToolResponse(false, $"'{path}' not found");

            var cmd = new StringBuilder();
            cmd.AppendFormat("-Orientation={0}\n-n\n-s3\n", ori);

            if(overwriteOriginal)
                cmd.Append("-overwrite_original\n");

            cmd.Append(path);
            var cmdRes = SendCommand(cmd.ToString());

            return cmdRes ? new ExifToolResponse(cmdRes.Result) : cmdRes;
        }

        public ExifToolResponse SetOrientationDeg(string path, int ori, bool overwriteOriginal = true) =>
            SetOrientation(path, OrientationDeg2Pos(ori), overwriteOriginal);

        public ExifToolResponse SetDescription(string path, string description, bool overwriteOriginal = true)
        {
            if (!File.Exists(path))
                return new ExifToolResponse(false, $"'{path}' not found");

            var prevMethod = Method;
            Method = CommunicationMethod.ViaFile;
            var res = SetExifInto(path, "XMP-dc:Description", description, overwriteOriginal);
            //var res = SendCommand($"-XMP-dc:Description={description}\n{path}");
            Method = prevMethod;

            return res;
        }

        public string GetDescription(string path)
        {
            if (!File.Exists(path))
                return null;

            var cmdRes = SendCommand("-XMP-dc:Description\n-n\n-s3\n{0}", path);
            if (!cmdRes)
                return "";

            return cmdRes.Result.Trim('\t', '\r', '\n');

            //var cmdRes = FetchExifFrom(path, { "XMP-dc:Description"});
            //return cmdRes.SingleOrDefault().Value ?? "";
        }

        #region Static orientation helpers

        /*
         
1        2       3      4         5            6           7          8

888888  888888      88  88      8888888888          88  8888888888   88          
88          88      88  88      88  88          88  88      88  88   88  88      
8888      8888    8888  8888    88          8888888888          88   8888888888  
88          88      88  88
88          88  888888  888888

        1 => 'Horizontal (normal)',
        2 => 'Mirror horizontal',
        3 => 'Rotate 180',
        4 => 'Mirror vertical',
        5 => 'Mirror horizontal and rotate 270 CW',
        6 => 'Rotate 90 CW',
        7 => 'Mirror horizontal and rotate 90 CW',
        8 => 'Rotate 270 CW'
         */

        public static int OrientationPos2Deg(int pos)
        {
            switch(pos)
            {
                case 8:
                    return 270;
                case 3:
                    return 180;
                case 6:
                    return 90;
                default:
                    return 0;
            }
        }

        public static int OrientationDeg2Pos(int deg)
        {
            switch(deg)
            {
                case 270:
                    return 8;
                case 180:
                    return 3;
                case 90:
                    return 6;
                default:
                    return 1;
            }
        }

        public static int OrientationString2Deg(string pos)
        {
            switch(pos)
            {
                case "Rotate 270 CW":
                    return 270;
                case "Rotate 180":
                    return 180;
                case "Rotate 90 CW":
                    return 90;
                default:
                    return 0;
            }
        }

        public static string OrientationDeg2String(int deg)
        {
            switch(deg)
            {
                case 270:
                    return "Rotate 270 CW";
                case 180:
                    return "Rotate 180";
                case 90:
                    return "Rotate 90 CW";
                default:
                    return "Horizontal (normal)";
            }
        }

        private static readonly int[] OrientationPositions = { 1, 6, 3, 8 };

        public static int RotateOrientation(int crtOri, bool clockwise, int steps = 1)
        {
            int newOri = 1;
            int len = OrientationPositions.Length;

            if(steps % len == 0)
                return crtOri;

            for(int i = 0; i < len; i++)
            {
                if(crtOri == OrientationPositions[i])
                {
                    newOri = clockwise
                        ? OrientationPositions[(i + steps) % len]
                        : OrientationPositions[(i + (1 + steps / len) * len - steps) % OrientationPositions.Length];

                    break;
                }
            }

            return newOri;
        }

        #endregion
        
        #region IDisposable Members

        public void Dispose()
        {
            Debug.Assert(Status == ExeStatus.Ready || Status == ExeStatus.Stopped, "Invalid state");

            if(_proc != null && Status == ExeStatus.Ready)
                Stop();

            _waitHandle.Dispose();
        }

        #endregion
    }
}
