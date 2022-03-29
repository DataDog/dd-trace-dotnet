using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using TinyGet.Config;
using TinyGet.Requests;

namespace TinyGet.Tests
{
    [TestFixture]
    public class AppHostTests
    {
        private Mock<IRequestSenderCreator> _requestSenderCreator;
        private Context _context;
        private AppHost _host;
        private CancellationTokenSource _cancellationTokenSource;
        private Mock<IAppArguments> _appArguments;

        [SetUp]
        public void Setup()
        {
            _requestSenderCreator = new Mock<IRequestSenderCreator>();
            _cancellationTokenSource = new CancellationTokenSource();
            _appArguments = new Mock<IAppArguments>();
            _context = new Context(_appArguments.Object, _cancellationTokenSource.Token, null);
            _host = new AppHost(_context, _requestSenderCreator.Object);
        }
        
        [Test]
        public void Should_Create_Senders_And_Call_Run()
        {
            // Arrange
            _appArguments.Setup(a => a.Threads).Returns(10);
            
            Mock<IRequestSender> sender = new Mock<IRequestSender>();
            sender.Setup(s => s.Run()).Returns(Task.FromResult(0));

            _requestSenderCreator.Setup(c => c.Create(_context)).Returns(sender.Object);

            // Act
            _host.Run();

            // Assert
            _requestSenderCreator.Verify(r => r.Create(_context), Times.Exactly(10));
            sender.Verify(s => s.Run(), Times.Exactly(10));
        }
    }
}
