using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using TinyGet.Config;

namespace TinyGet.Tests.Config
{
    [TestFixture]
    public class StringExtensionsTests
    {
        [Test]
        public void ToNameValueCollection_Should_Return_Empty_Object_For_Empty_Input()
        {
            // Arrange
            String[] emptyArray = new string[] { };

            // Act
            NameValueCollection result = emptyArray.ToNameValueCollection();

            // Assert
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void ToNameValueCollection_Should_Return_Empty_Object_For_Null_Input()
        {
            // Arrange
            String[] emptyArray = null;

            // Act
            NameValueCollection result = emptyArray.ToNameValueCollection();

            // Assert
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void ToNameValueCollection_Should_Convert_Array_To_Name_Value_Collection()
        {
            // Arrange
            String[] emptyArray = new[] {"-test:1", "-console", "-window:", ":argument", "", "-repeat", "-repeat:2"};

            // Act
            NameValueCollection result = emptyArray.ToNameValueCollection();

            // Assert
            Assert.That(result.Count, Is.EqualTo(5));
            Assert.That(result["-test"], Is.EqualTo("1"));
            Assert.That(result["-console"], Is.Empty);
            Assert.That(result["-window"], Is.Empty);
            Assert.That(result["argument"], Is.Empty);
            Assert.That(result["-repeat"], Is.EqualTo(",2"));
        }
    }
}
