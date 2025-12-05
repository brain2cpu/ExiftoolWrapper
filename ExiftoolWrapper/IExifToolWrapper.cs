using System;
using System.Collections.Generic;

namespace Brain2CPU.ExifTool;

public interface IExifToolWrapper : IDisposable
{
    double SecondsToWaitForError { get; set; }
    double SecondsToWaitForStop { get; set; }
    string ExifToolPath { get; }
    string ExifToolVersion { get; }
    ExifToolWrapper.ExeStatus Status { get; }
    ExifToolWrapper.CommunicationMethod Method { get; set; }
    bool Resurrect { get; set; }
    void Start();
    void Stop();
    ExifToolResponse SendCommand(string cmd, params object[] args);
    ExifToolResponse SetExifInto(string path, string key, string val, bool overwriteOriginal = true);
    ExifToolResponse SetExifInto(string path, Dictionary<string, string> data, bool overwriteOriginal = true);
    Dictionary<string, string> FetchExifFrom(string path, IEnumerable<string> tagsToKeep = null, bool keepKeysWithEmptyValues = true);
    List<string> FetchExifToListFrom(string path, IEnumerable<string> tagsToKeep = null, bool keepKeysWithEmptyValues = true, string separator = ": ");
    ExifToolResponse CloneExif(string source, string dest, bool backup = false);
    ExifToolResponse ClearExif(string path, bool backup = false);
    DateTime? GetCreationTime(string path);
    int GetOrientation(string path);
    int GetOrientationDeg(string path);
    ExifToolResponse SetOrientation(string path, int ori, bool overwriteOriginal = true);
    ExifToolResponse SetOrientationDeg(string path, int ori, bool overwriteOriginal = true);
    ExifToolResponse SetDescription(string path, string description, bool overwriteOriginal = true);
    string GetDescription(string path);
}