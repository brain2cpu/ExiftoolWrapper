using System;
using Brain2CPU.ExifTool;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestProject;

[TestClass]
public class ExifToolWrapperTests
{
    private const string TestFile = "test.jpg";
    private const string TestFile2 = "test2.jpg";

    private static ExifToolWrapper _exif;

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        _exif = new ExifToolWrapper();
        _exif.Start();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _exif.Stop();
        _exif.Dispose();
    }

    [TestMethod]
    public void Start_MissingExifTool_Throws()
    {
        Assert.Throws<ExifToolException>(() => new ExifToolWrapper("na"));
    }

    [TestMethod]
    public void StartStop_CorrectStatus()
    {
        var exif = new ExifToolWrapper();
        
        Assert.AreEqual(ExifToolWrapper.ExeStatus.Stopped, exif.Status);
        
        exif.Start();
        
        Assert.AreEqual(ExifToolWrapper.ExeStatus.Ready, exif.Status);
        Assert.IsNotEmpty(exif.ExifToolVersion);
        Assert.Contains("-stay_open True -@", _exif.ArgumentsInUse);
        
        exif.Stop();

        Assert.AreEqual(ExifToolWrapper.ExeStatus.Stopped, exif.Status);

        exif.Dispose();
    }
    
    [TestMethod]
    public void FetchExifFrom_ExpectedBehavior()
    {
        var v = _exif.FetchExifFrom(TestFile);
        Assert.AreEqual("100", v["ISO"]);
        
        v = _exif.FetchExifFrom(TestFile, ["ISO"]);
        Assert.ContainsSingle(v);
        Assert.AreEqual("100", v["ISO"]);
    }

    [TestMethod]
    public void FetchExifToListFrom_ExpectedBehavior()
    {
        var v = _exif.FetchExifToListFrom(TestFile);
        Assert.Contains("ISO: 100", v);
    }
    
    [TestMethod]
    public void GetCreationTime_ExpectedBehavior()
    {
        var d = _exif.GetCreationTime(TestFile);
        Assert.IsTrue(d.HasValue);
        Assert.AreEqual(new DateTime(2017, 7, 31, 14, 1, 0), d.Value);
    }

    [TestMethod]
    public void WriteOperations_ExpectedBehavior()
    {
        // orientation
        var oKey = "Orientation";
        var v = _exif.FetchExifFrom(TestFile2, [oKey]);
        Assert.IsNotEmpty(v);
        
        var r = _exif.SetOrientation(TestFile2, 3);
        Assert.IsTrue(r.IsSuccess);
        Assert.AreEqual("1 image files updated", r.Result.Trim().Trim('\t', '\r', '\n').ToLowerInvariant());
        
        v = _exif.FetchExifFrom(TestFile2, [oKey]);
        Assert.AreEqual("Rotate 180", v[oKey]);
        var o = _exif.GetOrientation(TestFile2);
        Assert.AreEqual(3, o);
        o = _exif.GetOrientationDeg(TestFile2);
        Assert.AreEqual(180, o);
        
        r = _exif.SetOrientationDeg(TestFile2, 90);
        Assert.IsTrue(r.IsSuccess);

        o = _exif.GetOrientationDeg(TestFile2);
        Assert.AreEqual(90, o);
        o = _exif.GetOrientation(TestFile2);
        Assert.AreEqual(6, o);
        
        // set exif
        var key = "Copyright";
        r = _exif.SetExifInto(TestFile2, key, "BB");
        Assert.IsTrue(r.IsSuccess);

        v = _exif.FetchExifFrom(TestFile2);
        Assert.AreEqual("BB", v[key]);

        r = _exif.SetExifInto(TestFile2, key, null);
        Assert.IsTrue(r.IsSuccess);

        v = _exif.FetchExifFrom(TestFile2);
        Assert.IsFalse(v.ContainsKey(key));
        var keyCount = v.Count;
        
        // clear
        r = _exif.ClearExif(TestFile2);
        Assert.IsTrue(r.IsSuccess);

        v = _exif.FetchExifFrom(TestFile2);
        Assert.IsFalse(v.ContainsKey(oKey));
        // some properties are always returned so v is not empty
        Assert.IsLessThan(keyCount, v.Count);

        // description
        var descr = "Description with accents ÁÅßéőş";
        r = _exif.SetDescription(TestFile2, descr);
        Assert.IsTrue(r.IsSuccess);
        Assert.AreEqual(descr, _exif.GetDescription(TestFile2));
        
        // clone
        r = _exif.CloneExif(TestFile, TestFile2);
        Assert.IsTrue(r.IsSuccess);

        var expected = _exif.FetchExifFrom(TestFile, [key]);
        v = _exif.FetchExifFrom(TestFile2);
        Assert.AreEqual(expected[key], v[key]);
        Assert.IsTrue(v.ContainsKey(oKey));
    }
}