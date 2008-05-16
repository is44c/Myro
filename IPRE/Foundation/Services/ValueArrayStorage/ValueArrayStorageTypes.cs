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
using System;
using System.Collections.Generic;
using W3C.Soap;
using valuearraystorage = Robotics.ValueArrayStorage;


namespace Robotics.ValueArrayStorage
{
    
    
    /// <summary>
    /// ValueArrayStorage Contract class
    /// </summary>
    public sealed class Contract
    {
        
        /// <summary>
        /// The Dss Service contract
        /// </summary>
        [DataMember()]
        public const String Identifier = "http://schemas.tempuri.org/2008/05/valuearray.html";
    }
    
    /// <summary>
    /// The ValueArrayStorage State
    /// </summary>
    [DataContract()]
    public class ValueArrayStorageState
    {
        [DataMember]
        double[] Values { get; set; }
        public ValueArrayStorageState()
        {
            Values[0] = 1;
        }
    }
    
    /// <summary>
    /// ValueArrayStorage Main Operations Port
    /// </summary>
    [ServicePort()]
    public class ValueArrayStorageOperations : PortSet<DsspDefaultLookup, DsspDefaultDrop, Get>
    {
    }
    
    /// <summary>
    /// ValueArrayStorage Get Operation
    /// </summary>
    public class Get : Get<GetRequestType, PortSet<ValueArrayStorageState, Fault>>
    {
        
        /// <summary>
        /// ValueArrayStorage Get Operation
        /// </summary>
        public Get()
        {
        }
        
        /// <summary>
        /// ValueArrayStorage Get Operation
        /// </summary>
        public Get(Microsoft.Dss.ServiceModel.Dssp.GetRequestType body) : 
                base(body)
        {
        }
        
        /// <summary>
        /// ValueArrayStorage Get Operation
        /// </summary>
        public Get(Microsoft.Dss.ServiceModel.Dssp.GetRequestType body, Microsoft.Ccr.Core.PortSet<ValueArrayStorageState,W3C.Soap.Fault> responsePort) : 
                base(body, responsePort)
        {
        }
    }
}
