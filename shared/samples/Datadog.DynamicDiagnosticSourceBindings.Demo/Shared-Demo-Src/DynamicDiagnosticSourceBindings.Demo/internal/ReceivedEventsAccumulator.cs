using System;
using System.Text;

namespace DynamicDiagnosticSourceBindings.Demo
{
    internal class ReceivedEventsAccumulator
    {
        private readonly int _expectedMaxIteration;
        private readonly bool[] _receivedEvents;
        private int _receivedCount;

        public ReceivedEventsAccumulator(int expectedMaxIteration)
        {
            _expectedMaxIteration = expectedMaxIteration;
            _receivedCount = 0;

            _receivedEvents = new bool[_expectedMaxIteration];
            for (int i = 0; i < _expectedMaxIteration; i++)
            {
                _receivedEvents[i] = false;
            }
        }

        public double ReceivedProportion
        {
            get { return _receivedCount / (double) _expectedMaxIteration; }
        }

        public int ReceivedCount
        {
            get { return _receivedCount; }
        }

        public void SetReceived(int iteration)
        {
            ValidateIteration(iteration);

            if (_receivedEvents[iteration] == false)
            {
                _receivedCount++;
            }

            _receivedEvents[iteration] = true;
        }

        public bool GetReceived(int iteration)
        {
            ValidateIteration(iteration);
            return _receivedEvents[iteration];
        }

        public string GetReceivedVisual(int width)
        {
            const char trueChar = 'X';
            const char falseChar = '.';

            if (width < 1)
            {
                width = 1;
            }

            var visual = new StringBuilder();
            for (int i = 0; i < _expectedMaxIteration; i++)
            {
                visual.Append(_receivedEvents[i] ? trueChar : falseChar);

                if ((i + 1) % width == 0)
                {
                    visual.AppendLine();
                }
            }

            return visual.ToString();
        }

        private void ValidateIteration(int iteration)
        {
            if (iteration < 0 || _expectedMaxIteration <= iteration)
            {
                throw new ArgumentOutOfRangeException(nameof(iteration),
                                                     $"{nameof(iteration)} should be in range 0 <= {nameof(iteration)} < {_expectedMaxIteration}, but it is {iteration}.");
            }
        }
    }
}
