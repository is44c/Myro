﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dss.Core;
using Microsoft.Dss.Hosting;
using Microsoft.Dss.ServiceModel.Dssp;
using W3C.Soap;
using Microsoft.Ccr.Core;
using System.Threading;

namespace Myro.Utilities
{
    public static class RSUtils
    {
        /// <summary>
        /// Synchronous receive.
        /// Waits for a response, returning T on success, and throwing an
        /// exception created by ExceptionOfFault on failure.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="port"></param>
        /// <returns></returns>
        public static T ReceiveSync<T>(DispatcherQueue taskQueue, PortSet<T, Fault> port, int timeout)
        {
            lock (taskQueue)
            {
                T ret = default(T);
                Fault error = null;

                //Console.WriteLine("ReceiveSync: starting");
                ManualResetEvent signal = new ManualResetEvent(false);
                Arbiter.Activate(taskQueue,
                    Arbiter.Choice<T, Fault>(
                        port,
                        delegate(T state)
                        {
                            //Console.WriteLine("ReceiveSync: got a " + state.GetType());
                            ret = state;
                            signal.Set();
                        },
                        delegate(Fault failure)
                        {
                            error = failure;
                            signal.Set();
                        }));
                if (signal.WaitOne(timeout, false))
                {
                    //Console.WriteLine("ReceiveSync: back!");
                    ThrowIfFaultNotNull(error);
                    return ret;
                }
                else
                {
                    //Console.WriteLine("ReceiveSync: timed out!");
                    throw (new ReceiveTimedOutException());
                }
            }
        }

        /// <summary>
        /// Synchronous receive, using DssEnvironment.TaskQueue.
        /// Waits for a response, returning T on success, and throwing an
        /// exception created by ExceptionOfFault on failure.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="port"></param>
        /// <returns></returns>
        public static T ReceiveSync<T>(PortSet<T, Fault> port)
        {
            return ReceiveSync(port, -1);
        }

        /// <summary>
        /// Synchronous receive, using DssEnvironment.TaskQueue.
        /// Waits for a response, returning T on success, and throwing an
        /// exception created by ExceptionOfFault on failure.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="port"></param>
        /// <returns></returns>
        public static T ReceiveSync<T>(PortSet<T, Fault> port, int timeout)
        {
            return ReceiveSync(DssEnvironment.TaskQueue, port, timeout);
        }

        /// <summary>
        /// Synchronous receive.
        /// Waits for a response, returning T on success, and throwing an
        /// exception created by ExceptionOfFault on failure.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="port"></param>
        /// <returns></returns>
        public static T ReceiveSync<T>(DispatcherQueue taskQueue, PortSet<T, Fault> port)
        {
            return ReceiveSync(taskQueue, port, -1);
        }


        /// <summary>
        /// If fault is not null, throws an exception generated by
        /// ExceptionOfFault.
        /// </summary>
        /// <param name="fault"></param>
        public static void ThrowIfFaultNotNull(Fault fault)
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
                    fault.Detail.Any.Length > 0 && (fault.Detail.Any[0] is CloneableException))
                return ((CloneableException)fault.Detail.Any[0]).Exception;
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
            return new Fault() { Detail = new Detail() { Any = new object[] { new CloneableException(exception) } } };
        }

        /// <summary>
        /// This method searches for a primary or alternate contract of the
        /// service that is present in the contracts list.  Requires a 
        /// taskQueue to activate tasks on.  Throws NoContractFoundException
        /// in a Fault if one cannot be found.
        /// </summary>
        /// <param name="taskQueue"></param>
        /// <param name="service"></param>
        /// <param name="contracts"></param>
        /// <returns></returns>
        public static PortSet<ServiceInfoType, Fault> FindCompatibleContract(DispatcherQueue taskQueue, Uri service, List<string> contracts)
        {
            PortSet<ServiceInfoType, Fault> returnPort = new PortSet<ServiceInfoType, Fault>();

            PortSet<LookupResponse, Fault> responsePort = new PortSet<LookupResponse, Fault>();
            //Console.WriteLine("RSUtils: Querying " + service);
            DssEnvironment.ServiceForwarderUnknownType(service).PostUnknownType(
                new DsspDefaultLookup() { Body = new LookupRequestType(), ResponsePort = responsePort });
            Arbiter.Activate(taskQueue, Arbiter.Choice<LookupResponse, Fault>(
                responsePort,
                delegate(LookupResponse resp)
                {
                    try
                    {
                        //Console.WriteLine("RSUtils: Got response");
                        returnPort.Post(FindCompatibleContract(resp, contracts));
                    }
                    catch (NoContractFoundException e)
                    {
                        returnPort.Post(FaultOfException(e));
                    }
                },
                delegate(Fault failure)
                {
                    returnPort.Post(failure);
                }));

            return returnPort;
        }

        /// <summary>
        /// This method searches for a primary or alternate contract of the
        /// service that is present in the contracts list.  Requires a 
        /// taskQueue to activate tasks on.  Throws NoContractFoundException
        /// if one cannot be found.
        /// </summary>
        /// <param name="taskQueue"></param>
        /// <param name="service"></param>
        /// <param name="contracts"></param>
        /// <returns></returns>
        public static ServiceInfoType FindCompatibleContract(ServiceInfoType serviceRecord, List<string> contracts)
        {
            ServiceInfoType ret = null;
            int retIndex = Int32.MaxValue;

            // See if we can understand the primary contract
            int i = contracts.FindIndex(
                (s => serviceRecord.Contract.Equals(s, StringComparison.Ordinal)));
            if (i >= 0 && i < retIndex)
            {
                ret = serviceRecord;
                retIndex = i;
            }

            // Now try each alternate contract
            foreach (var part in serviceRecord.PartnerList)
                // Alternate contract services have a name of "AlternateContractService".
                if (part.Name.Name.StartsWith("AlternateContractService"))
                {
                    i = contracts.FindIndex(
                        (s => part.Contract.Equals(s, StringComparison.Ordinal)));
                    if (i >= 0 && i < retIndex)
                    {
                        ret = part;
                        retIndex = i;
                    }
                }
            if (ret != null)
                return ret;
            else
                throw new NoContractFoundException(serviceRecord, contracts);
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
            //Console.WriteLine("***" + this.ToString());
        }
        public override string ToString()
        {
            return Strings.FromFault(Fault);
        }
    }

    class CloneableException : ICloneable
    {
        public Exception Exception { get; private set; }
        public CloneableException(Exception e)
        {
            Exception = e;
        }
        public object Clone()
        {
            return new CloneableException(Exception);
        }
    }

    /// <summary>
    /// This is thrown by FindCompatibleContract when a compatible contract
    /// cannot be found.
    /// </summary>
    public class NoContractFoundException : Exception
    {
        public NoContractFoundException(ServiceInfoType goal, List<string> targets) :
            base("Could not find a contract of service " + goal.Service + " matching any compatible contracts: " +
                String.Concat((from t in targets
                select t + "   ").ToArray()))
        {
            //Console.WriteLine("***" + this.ToString());
        }
    }

    /// <summary>
    /// This is thrown when a ReceiveSync call times out.
    /// </summary>
    public class ReceiveTimedOutException : Exception { }
}
