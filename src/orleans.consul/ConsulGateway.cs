using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace src
{
    public class ConsulGateway : IDisposable
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task SetProperty(string ping, string value)
        {
            return Task.FromResult(0);
        }
    }
}
