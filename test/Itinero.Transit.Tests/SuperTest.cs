using Xunit.Abstractions;

namespace Itinero.Transit.Tests
{
    public class SuperTest
    {
        private readonly ITestOutputHelper _output;

        public SuperTest(ITestOutputHelper output)
        {
            
            _output = output;
        }
        
        // ReSharper disable once UnusedMember.Local
        public void Log(string s)
        {
            if (s == null)
            {
                _output.WriteLine("Got a null value to log");
                return;
            }
            _output.WriteLine(s);
        }
    }
}