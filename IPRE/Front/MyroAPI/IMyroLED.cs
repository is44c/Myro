using System;
using System.Collections.Generic;
using System.Text;

namespace Myro.API
{
    
    public interface IMyroLED
    {
        void setBinary(uint number);
        void setLED(string position, string state);
        void setLED(string position, int state);
    }
}
