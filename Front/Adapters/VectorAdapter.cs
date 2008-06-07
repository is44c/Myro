﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Dss.ServiceModel.Dssp;
using Microsoft.Dss.Hosting;
using Microsoft.Ccr.Core;
using W3C.Soap;
using vector = Myro.Services.Generic.Vector.Proxy;
using System.Threading;
using Myro.Utilities;

namespace Myro.Adapters
{
    /// <summary>
    /// This class provides access to a Vector service.  It also allows lookup
    /// of values by tag, by caching a local dictionary of tags and values.
    /// If the TagTimestamp member of the VectorState becomes more recent than
    /// the cached tag-value dictionary, the dictionary is rebuilt.
    /// TODO: Implement setting by tag
    /// </summary>
    public class VectorAdapter : IAdapter
    {
        public ServiceInfoType ServiceInfo { get; private set; }

        vector.VectorOperations opPort;

        Dictionary<string, int> indexCache = null;
        DateTime indexCacheTime = DateTime.Now;

        public VectorAdapter(ServiceInfoType serviceRecord)
        {
            ServiceInfo = serviceRecord;
            opPort = DssEnvironment.ServiceForwarder<vector.VectorOperations>(new Uri(serviceRecord.Service));
            if (opPort == null)
                throw new AdapterCreationException("Service forwarder port was null");
        }

        /// <summary>
        /// Retrieve the entire vector state.  State members may be null.
        /// </summary>
        /// <returns></returns>
        public vector.VectorState GetState()
        {
            vector.VectorState ret = null;
            Fault error = null;
            Object monitor = new Object();
            ManualResetEvent signal = new ManualResetEvent(false);
            Arbiter.Activate(DssEnvironment.TaskQueue,
                Arbiter.Choice<vector.VectorState, Fault>(
                    opPort.Get(),
                    delegate(vector.VectorState state)
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
            if (error != null)
                throw new AdapterOperationException(error);
            else
                return ret;
        }

        /// <summary>
        /// Return only the values from the vector state.
        /// </summary>
        /// <returns></returns>
        public double[] Get()
        {
            vector.VectorState state = GetState();
            return state.Values.ToArray();
        }

        /// <summary>
        /// Retrieve a single element from the state vector, with full safety
        /// checks.  Throws AdapterArgumentException and
        /// AdapterOperationException.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public double Get(int index)
        {
            vector.VectorState state = GetState();
            if (state.Values != null)
            {
                if (index < 0 || index >= state.Values.Count)
                    throw new AdapterArgumentException(Strings.IndexOutOfBounds(index, state.Values.Count));
                else
                    return state.Values[index];
            }
            else
                throw new AdapterOperationException(Strings.NotReady);
        }

        /// <summary>
        /// Retrieve a single element from the state vetor, by name, with full
        /// safety checks.  Throws AdapterArgumentException and
        /// AdapterOperationException.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        public double Get(string tag)
        {
            vector.VectorState state = GetState();
            CheckIndexCache(state);
            int index;
            try
            {
                index = indexCache[tag];
            }
            catch (KeyNotFoundException e)
            {
                throw new AdapterArgumentException(Strings.KeyNotFound(tag));
            }
            if (index > state.Values.Count)
                throw new AdapterOperationException(Strings.VectorTooShort);
            return state.Values[index];
        }

        /// <summary>
        /// Set a single element in the vector by name.
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="value"></param>
        public void Set(string tag, double value)
        {
            // Kind of a hack, get the state and build dictionary if haven't
            // done a Get yet.
            if (indexCache == null)
                CheckIndexCache(GetState());
            int index;
            try
            {
                index = indexCache[tag];
            }
            catch (KeyNotFoundException)
            {
                throw new AdapterArgumentException(Strings.KeyNotFound(tag));
            }
            Set(index, value);
        }

        /// <summary>
        /// Set a single element in the vector by index.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        public void Set(int index, double value)
        {
            Fault error = null;
            ManualResetEvent signal = new ManualResetEvent(false);
            Arbiter.Activate(DssEnvironment.TaskQueue,
                Arbiter.Choice<DefaultUpdateResponseType, Fault>(
                    opPort.SetByIndex(index, value, DateTime.Now),
                    delegate(DefaultUpdateResponseType success)
                    {
                        signal.Set();
                    },
                    delegate(Fault failure)
                    {
                        error = failure;
                        signal.Set();
                    }));
            signal.WaitOne();
            if (error != null)
            {
                String msg = "Fault in setting vector: ";
                foreach (var r in error.Reason)
                    msg += r.Value;
                DssEnvironment.LogError(msg);
                throw new AdapterArgumentException(Strings.IndexOutOfBounds(index, 9999));
            }
        }

        public void SetAll(double[] values)
        {
            Fault error = null;
            ManualResetEvent signal = new ManualResetEvent(false);
            Arbiter.Activate(DssEnvironment.TaskQueue,
                Arbiter.Choice<DefaultUpdateResponseType, Fault>(
                    opPort.SetAll(new List<double>(values), DateTime.Now),
                    delegate(DefaultUpdateResponseType success)
                    {
                        signal.Set();
                    },
                    delegate(Fault failure)
                    {
                        error = failure;
                        signal.Set();
                    }));
            signal.WaitOne();
            if (error != null)
            {
                String msg = "Fault in setting vector: ";
                foreach (var r in error.Reason)
                    msg += r.Value;
                Console.WriteLine(msg);
            }
        }

        private void CheckIndexCache(vector.VectorState state)
        {
            if (indexCache == null || state.TagTimestamp.CompareTo(indexCacheTime) > 0)
            {
                indexCache = new Dictionary<string, int>(state.Tags.Count);
                int max = state.Tags.Count > state.Values.Count ? state.Values.Count : state.Tags.Count;
                for (int i = 0; i < max; i++)
                    indexCache.Add(state.Tags[i], i);
            }
        }
    }
}
