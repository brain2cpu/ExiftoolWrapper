using System;
using Brain2CPU.ExifTool;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestProject
{
    [TestClass]
    public class ExifToolWrapperTests
    {
        private const string TestFile = "test.jpg";
        private const string TestFile2 = "test2.jpg";

        private ExifToolWrapper _exif;

        [TestInitialize]
        public void TestInitialize()
        {
            _exif = new ExifToolWrapper();
            _exif.Start();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _exif.Stop();
            _exif.Dispose();
        }

        [TestMethod]
        public void Start_ExpectedBehavior()
        {
            var exif = new ExifToolWrapper();
            exif.Start();

            Assert.AreEqual(ExifToolWrapper.ExeStatus.Ready, exif.Status);
            Assert.IsFalse(string.IsNullOrEmpty(exif.ExifToolVersion));

            exif.Dispose();
        }

        [TestMethod]
        [ExpectedException(typeof(ExifToolException))]
        public void Start_MissingExifTool()
        {
            using(var exif = new ExifToolWrapper("na"))
            { }
        }

        [TestMethod]
        public void Stop_ExpectedBehavior()
        {
            var exif = new ExifToolWrapper();
            exif.Start();
            exif.Stop();

            Assert.AreEqual(ExifToolWrapper.ExeStatus.Stopped, exif.Status);

            exif.Dispose();
        }

        [TestMethod]
        public void SendCommand_ExpectedBehavior()
        {
            var r = _exif.SendCommand("-Orientation\n-n\n-s3\n" + TestFile);

            Assert.IsTrue(r.IsSuccess);
            Assert.AreEqual("1", r.Result.Trim('\t', '\r', '\n'));
        }

        [TestMethod]
        public void SetExifInto_ExpectedBehavior()
        {
            var r = _exif.SetExifInto(TestFile, "Copyright", "BB");
            Assert.IsTrue(r.IsSuccess);

            var v = _exif.FetchExifFrom(TestFile);
            Assert.AreEqual("BB", v["Copyright"]);
        }

        [TestMethod]
        public void SetExifInto_RemoveKey_ExpectedBehavior()
        {
            var r = _exif.SetExifInto(TestFile, "Copyright", "BB");
            Assert.IsTrue(r.IsSuccess);

            var v = _exif.FetchExifFrom(TestFile);
            Assert.AreEqual("BB", v["Copyright"]);

            r = _exif.SetExifInto(TestFile, "Copyright", null);
            Assert.IsTrue(r.IsSuccess);

            v = _exif.FetchExifFrom(TestFile);
            Assert.IsFalse(v.ContainsKey("Copyright"));
        }

        [TestMethod]
        public void FetchExifFrom_ExpectedBehavior()
        {
            var v = _exif.FetchExifFrom(TestFile);
            Assert.AreEqual("100", v["ISO"]);
        }

        [TestMethod]
        public void FetchExifToListFrom_ExpectedBehavior()
        {
            var v = _exif.FetchExifToListFrom(TestFile);
            Assert.IsTrue(v.Contains("ISO: 100"));
        }

        [TestMethod]
        public void CloneExif_ExpectedBehavior()
        {
            _exif.SetExifInto(TestFile, "Copyright", "NA");

            var r = _exif.CloneExif(TestFile, TestFile2);
            Assert.IsTrue(r.IsSuccess);

            var v = _exif.FetchExifFrom(TestFile2);
            Assert.AreEqual("NA", v["Copyright"]);
        }

        [TestMethod]
        public void ClearExif_ExpectedBehavior()
        {
            var r = _exif.ClearExif(TestFile2);
            Assert.IsTrue(r.IsSuccess);

            var v = _exif.FetchExifFrom(TestFile2);
            Assert.IsFalse(v.ContainsKey("Copyright"));
        }

        [TestMethod]
        public void GetCreationTime_ExpectedBehavior()
        {
            var d = _exif.GetCreationTime(TestFile);
            Assert.IsTrue(d.HasValue);
            Assert.AreEqual(new DateTime(2017, 7, 31, 14, 1, 0), d.Value);
        }

        [TestMethod]
        public void GetOrientation_ExpectedBehavior()
        {
            var o = _exif.GetOrientation(TestFile);
            Assert.AreEqual(1, o);
        }

        [TestMethod]
        public void GetOrientationDeg_ExpectedBehavior()
        {
            var o = _exif.GetOrientationDeg(TestFile);
            Assert.AreEqual(0, o);
        }

        [TestMethod]
        public void SetOrientation_ExpectedBehavior()
        {
            var r = _exif.SetOrientation(TestFile2, 2);
            Assert.IsTrue(r.IsSuccess);

            var o = _exif.GetOrientation(TestFile2);
            Assert.AreEqual(2, o);
        }

        [TestMethod]
        public void SetOrientationDeg_ExpectedBehavior()
        {
            var r = _exif.SetOrientationDeg(TestFile2, 270);
            Assert.IsTrue(r.IsSuccess);

            var o = _exif.GetOrientationDeg(TestFile2);
            Assert.AreEqual(270, o);
        }

        [TestMethod]
        public void TypicalUsage_ExpectedBehavior()
        {
            using(var exif = new ExifToolWrapper())
            {
                exif.Start();

                var o = _exif.GetOrientation(TestFile);
                Assert.AreEqual(1, o);

                var v = _exif.FetchExifFrom(TestFile);
                Assert.IsTrue(v.Count > 0);
            }
        }
    }
}
