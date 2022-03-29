using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TinyGet.Config;

namespace TinyGet
{
    internal class Context
    {
        private readonly IAppArguments _arguments;
        private readonly CancellationToken _token;
        private readonly TextWriter _output;

        public Context(IAppArguments arguments, CancellationToken token, TextWriter output)
        {
            _arguments = arguments;
            _token = token;
            _output = output;
        }

        public IAppArguments Arguments
        {
            get { return _arguments; }
        }

        public CancellationToken Token
        {
            get { return _token; }
        }
    }
}
