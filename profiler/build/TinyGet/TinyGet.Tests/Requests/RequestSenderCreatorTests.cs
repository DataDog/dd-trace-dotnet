using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using TinyGet.Requests;

namespace TinyGet.Tests.Requests
{
    [TestFixture]
    public class RequestSenderCreatorTests
    {
        [Test]
        public void Should_Create_Request_Sender()
        {
            // Arrange
            RequestSenderCreator creator = new RequestSenderCreator();

            // Act
            IRequestSender result = creator.Create(null);

            // Assert
            Assert.That(result, Is.TypeOf<RequestSender>());
        }
    }
}
