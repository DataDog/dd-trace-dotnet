using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using TinyGet.Config;
using TinyGet.Requests;
using TinyGet.Tests.Controllers;
using TinyGet.Tests.Helpers;

namespace TinyGet.Tests.Requests
{
    [TestFixture]
    public class RequestSenderTests
    {
        private RequestSender _sender;
        private Mock<IAppArguments> _arguments;
        private CancellationTokenSource _tokenSource;
        private ApiServer _server;

        [SetUp]
        public void Setup()
        {
            RequestRecorder.Reset();

            _arguments = new Mock<IAppArguments>();
            _arguments.Setup(a => a.Method).Returns(HttpMethod.Get);
            _arguments.Setup(a => a.Status).Returns(200);
            _arguments.Setup(a => a.Loop).Returns(1);

            _tokenSource = new CancellationTokenSource();
            Context context = new Context(_arguments.Object, _tokenSource.Token, null);
           
            _sender = new RequestSender(context);
            _server = new ApiServer();
        }

        [TearDown]
        public void TearDown()
        {
            if (null != _server)
            {
                _server.Dispose();
            }
        }

        [Test]
        public void Should_Send_Http_Request()
        {           
            // Arrange                        
            _arguments.Setup(a => a.GetUrl()).Returns(_server.HostUrl + "api/Home");

            // Act
            Task result = _sender.Run();
            result.Wait();

            // Assert
            AssertSingleRequestMethod("HomeController.Get", 1);
        }

        [Test]
        [Timeout(5000)]
        public void Should_Cancel_Request()
        {
            // Arrange
            _arguments.Setup(a => a.GetUrl()).Returns(_server.HostUrl + "api/Home/long-running");

            // Act
            _tokenSource.Cancel();
            Task result = _sender.Run();

            // Assert
            Assert.Throws<AggregateException>(result.Wait);
            Assert.That(result.IsCanceled, Is.True);
        }

        [Test]
        public void Should_Check_Status_Code()
        {
            // Arrange                
            _arguments.Setup(a => a.GetUrl()).Returns(_server.HostUrl + "api/not-found");

            // Act
            Task task = _sender.Run();
            ApplicationException result = Assert.Throws<AggregateException>(task.Wait).InnerExceptions.Single() as ApplicationException;

            // Assert
            Assert.That(result.Message, Is.EqualTo("Status code is not equal to 200"));
        }

        [Test]
        public void Should_Send_Multiple_Requests()
        {
            // Arrange
            _arguments.Setup(a => a.Loop).Returns(10);
            _arguments.Setup(a => a.GetUrl()).Returns(_server.HostUrl + "api/Home");

            // Act
            Task result = _sender.Run();
            result.Wait();

            // Assert
            AssertSingleRequestMethod("HomeController.Get", 10);
        }

        private static void AssertSingleRequestMethod(string name, int count)
        {
            Assert.That(RequestRecorder.GetTotal(), Is.EqualTo(count));
            Assert.That(RequestRecorder.Get(name), Is.EqualTo(count));
        }
    }
}
