using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Saku_Overclock.Services;
public class SmuAddressSet
{
    public uint MsgAddress;
    public uint RspAddress;
    public uint ArgAddress;

    public SmuAddressSet()
    {
        MsgAddress = 0;
        RspAddress = 0;
        ArgAddress = 0;
    }

    public SmuAddressSet(uint msgAddress, uint rspAddress, uint argAddress)
    {
        MsgAddress = msgAddress;
        RspAddress = rspAddress;
        ArgAddress = argAddress;
    }
}
