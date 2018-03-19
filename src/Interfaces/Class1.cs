using System;
using System.Threading.Tasks;
using Orleans;

namespace Interfaces
{
    public interface IName : IGrainWithIntegerKey
    {
        Task SayHello();
    }
}
