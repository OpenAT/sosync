using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Common;

namespace Common.Tests
{
    [TestClass]
    public class EmailHelperTests
    {
        [TestMethod]
        public void SplitEmail_Empty_Works()
        {
            string input = "";
            string vorExpected = null;
            string nachExpected = null;
            string vorActual = null;
            string nachActual = null;

            EmailHelper.SplitEmail(input, out vorActual, out nachActual);

            Assert.AreEqual(vorExpected, vorActual);
            Assert.AreEqual(nachExpected, nachActual);
        }

        [TestMethod]
        public void SplitEmail_Null_Works()
        {
            string input = null;
            string vorExpected = null;
            string nachExpected = null;
            string vorActual = null;
            string nachActual = null;

            EmailHelper.SplitEmail(input, out vorActual, out nachActual);

            Assert.AreEqual(vorExpected, vorActual);
            Assert.AreEqual(nachExpected, nachActual);
        }

        [TestMethod]
        public void SplitEmail_ValidEmail_Works()
        {
            string input = "office@datadialog.net";
            string vorExpected = "office";
            string nachExpected = "datadialog.net";
            string vorActual = null;
            string nachActual = null;

            EmailHelper.SplitEmail(input, out vorActual, out nachActual);

            Assert.AreEqual(vorExpected, vorActual);
            Assert.AreEqual(nachExpected, nachActual);
        }

        [TestMethod]
        public void SplitEmail_InvalidEmailVor_Works()
        {
            string input = "@datadialog.net";
            string vorExpected = null;
            string nachExpected = "datadialog.net";
            string vorActual = null;
            string nachActual = null;

            EmailHelper.SplitEmail(input, out vorActual, out nachActual);

            Assert.AreEqual(vorExpected, vorActual);
            Assert.AreEqual(nachExpected, nachActual);
        }

        [TestMethod]
        public void SplitEmail_InvalidEmailNach_Works()
        {
            string input = "office@";
            string vorExpected = "office";
            string nachExpected = null;
            string vorActual = null;
            string nachActual = null;

            EmailHelper.SplitEmail(input, out vorActual, out nachActual);

            Assert.AreEqual(vorExpected, vorActual);
            Assert.AreEqual(nachExpected, nachActual);
        }

        [TestMethod]
        public void SplitEmail_InvalidEmail_NoAtSign_Works()
        {
            string input = "office";
            string vorExpected = "office";
            string nachExpected = null;
            string vorActual = null;
            string nachActual = null;

            EmailHelper.SplitEmail(input, out vorActual, out nachActual);

            Assert.AreEqual(vorExpected, vorActual);
            Assert.AreEqual(nachExpected, nachActual);
        }

        [TestMethod]
        public void MergeEmail_AllNull_Works()
        {
            string inputVor = null;
            string inputNach = null;

            string expected = null;
            string actual = null;

            actual = EmailHelper.MergeEmail(inputVor, inputNach);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MergeEmail_AllEmpty_Works()
        {
            string inputVor = "";
            string inputNach = "";

            string expected = null;
            string actual = null;

            actual = EmailHelper.MergeEmail(inputVor, inputNach);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MergeEmail_NachNull_Works()
        {
            string inputVor = "office";
            string inputNach = null;

            string expected = "office";
            string actual = null;

            actual = EmailHelper.MergeEmail(inputVor, inputNach);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MergeEmail_NachEmpty_Works()
        {
            string inputVor = "office";
            string inputNach = "";

            string expected = "office";
            string actual = null;

            actual = EmailHelper.MergeEmail(inputVor, inputNach);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MergeEmail_VorNull_Works()
        {
            string inputVor = null;
            string inputNach = "datadialog.net";

            string expected = "datadialog.net";
            string actual = null;

            actual = EmailHelper.MergeEmail(inputVor, inputNach);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MergeEmail_VorEmpty_Works()
        {
            string inputVor = "";
            string inputNach = "datadialog.net";

            string expected = "datadialog.net";
            string actual = null;

            actual = EmailHelper.MergeEmail(inputVor, inputNach);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MergeEmail_Valid_Works()
        {
            string inputVor = "office";
            string inputNach = "datadialog.net";

            string expected = "office@datadialog.net";
            string actual = null;

            actual = EmailHelper.MergeEmail(inputVor, inputNach);

            Assert.AreEqual(expected, actual);
        }
    }
}
