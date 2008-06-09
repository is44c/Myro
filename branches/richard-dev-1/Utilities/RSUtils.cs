﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dss.Core;
using Microsoft.Dss.Hosting;
using W3C.Soap;
using Microsoft.Ccr.Core;
using System.Threading;

namespace Myro.Utilities
{
    public static class RSUtils
    {
        /// <summary>
        /// Synchronous receive, using DssEnvironment.TaskQueue.
        /// Waits for a 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="port"></param>
        /// <returns></returns>
        public static T RecieveSync<T>(PortSet<T, Fault> port)
        {
            T ret = default(T);
            Fault error = null;
            
            ManualResetEvent signal = new ManualResetEvent(false);
            Arbiter.Activate(DssEnvironment.TaskQueue,
                Arbiter.Choice<T, Fault>(
                    port,
                    delegate(T state)
                    {
                        ret = state;
                        signal.Set();
                    },
                    delegate(Fault failure)
                    {
                        error = failure;
                        signal.Set();
                    }));
            signal.WaitOne();

            ThrowIfFault(error);
            return ret;
        }

        /// <summary>
        /// If fault is not null, throws an exception generated by
        /// ExceptionOfFault.
        /// </summary>
        /// <param name="fault"></param>
        public static void ThrowIfFault(Fault fault)
        {
            if (fault != null)
                throw ExceptionOfFault(fault);
        }

        /// <summary>
        /// Generates an exception from a Fault.  If the Fault encapsulates an
        /// exception (Fault.Detail.Any[0] is Exception), as do Faults created
        /// by FaultOfException, this method returns the exception.  Otherwise,
        /// it returns a FaultReceivedException, which encapsulates the Fault.
        /// </summary>
        /// <param name="fault"></param>
        /// <returns></returns>
        public static Exception ExceptionOfFault(Fault fault)
        {
            if (fault.Detail != null && fault.Detail.Any != null &&
                    fault.Detail.Any.Length > 0 && (fault.Detail.Any[0] is Exception))
                return (Exception)fault.Detail.Any[0];
            else
                return new FaultReceivedException(fault);
        }

        /// <summary>
        /// Generates a Fault that encapsulates an exception, by making
        /// Fault.Detail.Any[0] = exception.  The original exception can by
        /// retreived by calling ExceptionOfFault.
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public static Fault FaultOfException(Exception exception)
        {
            return new Fault() { Detail = new Detail() { Any = new object[] { exception } } };
        }
    }

    /// <summary>
    /// An exception that encapsulates a fault, which is thrown if a fault is
    /// generated that itself does not encapsulate an exception (such as a 
    /// fault generated by the DSS system).  If a Fault does encapsulate an
    /// exception, that exception is thrown instead (using ExceptionOfFault()).
    /// </summary>
    public class FaultReceivedException : Exception
    {
        public Fault Fault { get; private set; }
        public FaultReceivedException(Fault fault)
        {
            Fault = fault;
        }
        public string ToString()
        {
            return Strings.FromFault(Fault);
        }
    }
}
