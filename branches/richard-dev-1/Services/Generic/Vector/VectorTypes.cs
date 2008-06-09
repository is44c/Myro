//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     DSS Runtime Version: 2.0.730.3
//     CLR Runtime Version: 2.0.50727.1434
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using Microsoft.Ccr.Core;
using Microsoft.Dss.Core.Attributes;
using Microsoft.Dss.ServiceModel.Dssp;
using Microsoft.Dss.Core.DsspHttp;
using System;
using System.Linq;
using System.Collections.Generic;
using W3C.Soap;

namespace Myro.Services.Generic.Vector
{
    /// <summary>
    /// Vector Contract class
    /// </summary>
    public sealed class Contract
    {
        /// <summary>
        /// The Dss Service contract
        /// </summary>
        [DataMember()]
        public const String Identifier = "http://schemas.tempuri.org/2008/06/vector.html";
    }

    /// <summary>
    /// The Vector state.  This consists of a list of values, containing 
    /// floating point numbers of type double.  Each value is referred to as an
    /// element of the vector.  There is an optional list of string keys as
    /// well, and if provided, elements can be queried by key.  Elements can be
    /// queried by index, or retrieved all at once, and if keys are present, 
    /// elements can also be queried by key.  Likewise, elements can be set by
    /// index or key.
    /// </summary>
    [DataContract()]
    public class VectorState
    {
        #region VectorState Properties
        /// <summary>
        /// The list of values in the vector.  This list does not need to keep
        /// a constant length.  Do not modify this list directly, instead use
        /// the public methods of VectorState.
        /// </summary>
        [DataMember()]
        public List<double> Values { get; set; }

        /// <summary>
        /// The list of keys referring to vector elements.  This can be an
        /// empty list, or can be shorter or longer than the list of elements.
        /// For example, if there are 10 elements and only 5 keys, the first
        /// 5 elements will be accessible by key.  If there are 5 elements but
        /// 6 keys, the last key will not be used, and will generate a
        /// KeyNotFoundException if it is used in GetByKey or SetByKey. DO NOT
        /// modify this list directly, instead use the public methods of
        /// VectorState.
        /// </summary>
        [DataMember()]
        public List<string> Keys { get; set; }

        /// <summary>
        /// A cache to make key lookups faster.  Do not modify this.
        /// </summary>
        [DataMember()]
        public Dictionary<string, int> indexCache { get; set; }

        /// <summary>
        /// The time of the last modification of any element in the vector.
        /// </summary>
        [DataMember()]
        public DateTime Timestamp { get; set; }
        #endregion

        #region VectorState Constructors
        /// <summary>
        /// Default constructor, creates an empty vector.
        /// </summary>
        public VectorState() :
            this(null, null, DateTime.Now) { }

        /// <summary>
        /// Creates a vector from the values parameter, and sets the last
        /// modification timestamp to timestamp.
        /// </summary>
        /// <param name="values"></param>
        /// <param name="timestamp"></param>
        public VectorState(List<Double> values, DateTime timestamp) : this(values, null, timestamp) { }

        /// <summary>
        /// Creates a vector from the values parameter, and sets the last 
        /// modification timestamp to the current time.
        /// </summary>
        /// <param name="values"></param>
        public VectorState(List<Double> values) : this(values, null, DateTime.Now) { }

        /// <summary>
        /// Creates a vector by converting the boolean values, converting false
        /// to 0.0, and true to 1.0.
        /// </summary>
        /// <param name="values"></param>
        /// <param name="timestamp"></param>
        public VectorState(IList<bool> values, DateTime timestamp) :
            this(from v in values select (v ? 1.0 : 0.0), new List<string>(), timestamp) { }

        /// <summary>
        /// Creates a vector from the values parameter, associating each value
        /// with the corresponding string key in keys, and sets the last 
        /// modification timestamp to timestamp.
        /// </summary>
        /// <param name="values"></param>
        /// <param name="keys"></param>
        /// <param name="timestamp"></param>
        public VectorState(IEnumerable<double> values, IEnumerable<string> keys, DateTime timestamp)
        {
            Values = (values == null ? new List<double>() : new List<double>(values));
            Keys = (keys == null ? new List<string>() : new List<string>(keys));
            Timestamp = (timestamp == null ? DateTime.Now : timestamp);
            rebuildIndexCache();
        }
        #endregion

        #region VectorState public accessor and modifier methods
        public double Get(int index) { return Values[index]; }
        public double Get(string key) { return Values[indexCache[key]]; }
        public IList<double> GetValues() { return Values; }
        public IList<bool> GetValuesBool() { return new List<bool>(from v in Values select (v >= 0.5 ? true : false)); }
        public void Set(int index, double value) { Values[index] = value; }
        public void Set(string key, double value) { Set(indexCache[key], value); }
        public void SetAll(IEnumerable<bool> values) { SetAll(from v in values select (v ? 1.0 : 0.0)); }
        public void SetAll(IEnumerable<double> values)
        {
            List<double> newValues = new List<double>(values);
            // If the length of the vector changes, rebuild the index cache
            // because the number of elements accessible by key is the lesser
            // of the number of elements and the number of keys.
            if (Values.Count != newValues.Count)
            {
                Values = newValues;
                rebuildIndexCache();
            }
            else
                Values = new List<double>(values);
        }
        #endregion

        #region Private methods
        private void rebuildIndexCache()
        {
            indexCache = new Dictionary<string, int>(Keys.Count);
            int max = Keys.Count > Values.Count ? Values.Count : Keys.Count;
            for (int i = 0; i < max; i++)
                indexCache.Add(Keys[i], i);
        }
        #endregion
    }

    /// <summary>
    /// Vector Main Operations Port
    /// </summary>
    [ServicePort()]
    public class VectorOperations : PortSet<DsspDefaultLookup, DsspDefaultDrop,
        HttpGet, Get, Replace, GetByIndex, GetByKey, SetByIndex, SetByKey, SetAll, Subscribe>
    {
    }

    #region Standard DSS Operations
    public class Get : Get<GetRequestType, PortSet<VectorState, Fault>>
    {
        public Get() : base() { }
        public Get(GetRequestType body) : base(body) { }
        public Get(GetRequestType body, PortSet<VectorState, Fault> responsePort) : base(body, responsePort) { }
    }

    public class Subscribe : Subscribe<SubscribeRequestType, DsspResponsePort<SubscribeResponseType>>
    {
        public Subscribe() : base() { }
        public Subscribe(SubscribeRequestType body) : base(body) { }
        public Subscribe(SubscribeRequestType body, DsspResponsePort<SubscribeResponseType> responsePort) : base(body, responsePort) { }
    }

    public class Replace : Replace<VectorState, DsspResponsePort<DefaultReplaceResponseType>>
    {
        public Replace() : base() { }
        public Replace(VectorState body) : base(body) { }
        public Replace(VectorState body, DsspResponsePort<DefaultReplaceResponseType> responsePort) : base(body, responsePort) { }
    }
    #endregion


    #region Specialized DSS Operations
    /// <summary>
    /// The response for GetByIndex and GetByKey.  Contains the requested 
    /// element and timestamp.
    /// </summary>
    [DataContract]
    [DataMemberConstructor]
    public class GetElementResponseType
    {
        /// <summary>
        /// The value of the element requested in GetByIndex or GetByKey.
        /// </summary>
        [DataMember]
        public double Value { get; set; }
        [DataMember]
        public DateTime Timestamp { get; set; }
        public GetElementResponseType()
        {
            Value = 0.0;
            Timestamp = DateTime.Now;
        }
    }


    /// <summary>
    /// A request to retrieve an element by index.  Normally, a
    /// GetElementResponseType will be returned containing the requested
    /// element and the last modification timestamp.  If the index is out of
    /// bounds, a Fault will be returned that encapsulates an 
    /// ArgumentOutOfBounds exception, which may be retrieved using 
    /// RSUtils.ExceptionOfFault.
    /// </summary>
    [DataContract]
    [DataMemberConstructor]
    public class GetByIndexRequestType
    {
        [DataMember]
        public int Index { get; set; }
        public GetByIndexRequestType()
        {
            Index = 0;
        }
    }
    public class GetByIndex : Update<GetByIndexRequestType, DsspResponsePort<GetElementResponseType>>
    {
        public GetByIndex() : base() { }
        public GetByIndex(GetByIndexRequestType body) : base(body) { }
        public GetByIndex(GetByIndexRequestType body, DsspResponsePort<GetElementResponseType> responsePort) : base(body, responsePort) { }
    }


    /// <summary>
    /// A request to retrieve an element by key.  Normally, a
    /// GetElementResponseType will be returned containing the requested
    /// element and the last modification timestamp.  If the key is not valid,
    /// a Fault will be returned that encapsulates a KeyNotFoundException,
    /// which may be retrieved using RSUtils.ExceptionOfFault.
    /// </summary>
    [DataContract]
    [DataMemberConstructor]
    public class GetByKeyRequestType
    {
        [DataMember]
        public String Key { get; set; }
        public GetByKeyRequestType()
        {
            Key = "";
        }
    }
    public class GetByKey : Update<GetByKeyRequestType, DsspResponsePort<GetElementResponseType>>
    {
        public GetByKey() : base() { }
        public GetByKey(GetByKeyRequestType body) : base(body) { }
        public GetByKey(GetByKeyRequestType body, DsspResponsePort<GetElementResponseType> responsePort) : base(body, responsePort) { }
    }


    /// <summary>
    /// A request to modify an element by index.  You must also provide a 
    /// timestamp, which could be the time the data was actually generated,
    /// or could just be the current time, retrieved with DateTime.Now.  The
    /// timestamp is not used internally.  The last modification timestamp of 
    /// the vector will be set to this timestamp, and the timestamp from the 
    /// last "Set" operation will be returned with a "Get" operation.  If the
    /// index is out of bounds, a Fault will be returned that encapsulates an 
    /// ArgumentOutOfBounds exception, which may be retrieved using 
    /// RSUtils.ExceptionOfFault.
    /// </summary>
    [DataContract]
    [DataMemberConstructor]
    public class SetByIndexRequestType
    {
        [DataMember]
        public int Index { get; set; }
        [DataMember]
        public double Value { get; set; }
        [DataMember]
        public DateTime Timestamp { get; set; }
        public SetByIndexRequestType()
        {
            Index = 0;
            Value = 0.0;
            Timestamp = DateTime.Now;
        }
    }
    public class SetByIndex : Update<SetByIndexRequestType, DsspResponsePort<DefaultUpdateResponseType>>
    {
        public SetByIndex() : base() { }
        public SetByIndex(SetByIndexRequestType body) : base(body) { }
        public SetByIndex(SetByIndexRequestType body, DsspResponsePort<DefaultUpdateResponseType> responsePort) : base(body, responsePort) { }
    }


    /// <summary>
    /// A request to modify an element by key.  You must also provide a 
    /// timestamp, which could be the time the data was actually generated,
    /// or could just be the current time, retrieved with DateTime.Now.  The
    /// timestamp is not used internally.  The last modification timestamp of 
    /// the vector will be set to this timestamp, and the timestamp from the 
    /// last "Set" operation will be returned with a "Get" operation.  If the
    /// key is not valid, a Fault will be returned that encapsulates a
    /// KeyNotFoundException, which may be retrieved using
    /// RSUtils.ExceptionOfFault.
    /// </summary>
    [DataContract]
    [DataMemberConstructor]
    public class SetByKeyRequestType
    {
        [DataMember]
        public string Key { get; set; }
        [DataMember]
        public double Value { get; set; }
        [DataMember]
        public DateTime Timestamp { get; set; }
        public SetByKeyRequestType()
        {
            Key = "";
            Value = 0.0;
            Timestamp = DateTime.Now;
        }
    }
    public class SetByKey : Update<SetByKeyRequestType, DsspResponsePort<DefaultUpdateResponseType>>
    {
        public SetByKey() : base() { }
        public SetByKey(SetByKeyRequestType body) : base(body) { }
        public SetByKey(SetByKeyRequestType body, DsspResponsePort<DefaultUpdateResponseType> responsePort) : base(body, responsePort) { }
    }


    /// <summary>
    /// A request to modify all elements at once.  You must also provide a 
    /// timestamp, which could be the time the data was actually generated,
    /// or could just be the current time, retrieved with DateTime.Now.  The
    /// timestamp is not used internally.  The last modification timestamp of 
    /// the vector will be set to this timestamp, and the timestamp from the 
    /// last "Set" operation will be returned with a "Get" operation.
    /// </summary>
    [DataContract]
    [DataMemberConstructor]
    public class SetAllRequestType
    {
        [DataMember]
        public List<double> Values { get; set; }
        [DataMember]
        public DateTime Timestamp { get; set; }
        public SetAllRequestType()
        {
            Values = new List<double>();
            Timestamp = DateTime.Now;
        }
    }
    public class SetAll : Update<SetAllRequestType, DsspResponsePort<DefaultUpdateResponseType>>
    {
        public SetAll() : base() { }
        public SetAll(SetAllRequestType body) : base(body) { }
        public SetAll(SetAllRequestType body, DsspResponsePort<DefaultUpdateResponseType> responsePort) : base(body, responsePort) { }
    }
    #endregion

}
