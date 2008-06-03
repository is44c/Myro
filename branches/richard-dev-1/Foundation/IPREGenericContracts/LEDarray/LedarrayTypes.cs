//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:2.0.50727.312
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using Microsoft.Ccr.Core;
using Microsoft.Dss.Core.Attributes;
using Microsoft.Dss.ServiceModel.Dssp;
using System;
using System.Collections;
using System.Collections.Generic;
using W3C.Soap;
using ledarray = IPREGenericContracts.LEDarray;


namespace IPREGenericContracts.LEDarray
{
    
    /// <summary>
    /// Ledarray Contract class
    /// </summary>
    public sealed class Contract
    {
        /// <summary>
        /// The Dss Service contract
        /// </summary>
        public const String Identifier = "http://schemas.tempuri.org/2007/06/ledarray.html";
    }
    /// <summary>
    /// The Ledarray State
    /// </summary>
    [DataContract()]
    public class LedarrayState
    {
        private List<LEDVector> _leds;

        /// <summary>
        /// The list of LEDs
        /// </summary>
        [DataMember]
        public List<LEDVector> LEDs
        {
            get { return this._leds; }
            set { this._leds = value; }
        }
    }
    /// <summary>
    /// Ledarray Main Operations Port
    /// </summary>
    public class LedarrayOperations : PortSet<DsspDefaultLookup, DsspDefaultDrop, Get, SetSingle, SetVector>
    {
        /// <summary>
        /// Required Lookup request body type
        /// </summary>
        public virtual Microsoft.Ccr.Core.PortSet<Microsoft.Dss.ServiceModel.Dssp.LookupResponse,W3C.Soap.Fault> DsspDefaultLookup()
        {
            Microsoft.Dss.ServiceModel.Dssp.LookupRequestType body = new Microsoft.Dss.ServiceModel.Dssp.LookupRequestType();
            Microsoft.Dss.ServiceModel.Dssp.DsspDefaultLookup op = new Microsoft.Dss.ServiceModel.Dssp.DsspDefaultLookup(body);
            this.Post(op);
            return op.ResponsePort;

        }
        /// <summary>
        /// Post Dssp Default Lookup and return the response port.
        /// </summary>
        public virtual Microsoft.Ccr.Core.PortSet<Microsoft.Dss.ServiceModel.Dssp.LookupResponse,W3C.Soap.Fault> DsspDefaultLookup(Microsoft.Dss.ServiceModel.Dssp.LookupRequestType body)
        {
            Microsoft.Dss.ServiceModel.Dssp.DsspDefaultLookup op = new Microsoft.Dss.ServiceModel.Dssp.DsspDefaultLookup();
            op.Body = body ?? new Microsoft.Dss.ServiceModel.Dssp.LookupRequestType();
            this.Post(op);
            return op.ResponsePort;

        }
        /// <summary>
        /// A request to drop the service.
        /// </summary>
        public virtual Microsoft.Ccr.Core.PortSet<Microsoft.Dss.ServiceModel.Dssp.DefaultDropResponseType,W3C.Soap.Fault> DsspDefaultDrop()
        {
            Microsoft.Dss.ServiceModel.Dssp.DropRequestType body = new Microsoft.Dss.ServiceModel.Dssp.DropRequestType();
            Microsoft.Dss.ServiceModel.Dssp.DsspDefaultDrop op = new Microsoft.Dss.ServiceModel.Dssp.DsspDefaultDrop(body);
            this.Post(op);
            return op.ResponsePort;

        }
        /// <summary>
        /// Post Dssp Default Drop and return the response port.
        /// </summary>
        public virtual Microsoft.Ccr.Core.PortSet<Microsoft.Dss.ServiceModel.Dssp.DefaultDropResponseType,W3C.Soap.Fault> DsspDefaultDrop(Microsoft.Dss.ServiceModel.Dssp.DropRequestType body)
        {
            Microsoft.Dss.ServiceModel.Dssp.DsspDefaultDrop op = new Microsoft.Dss.ServiceModel.Dssp.DsspDefaultDrop();
            op.Body = body ?? new Microsoft.Dss.ServiceModel.Dssp.DropRequestType();
            this.Post(op);
            return op.ResponsePort;

        }
        /// <summary>
        /// Required Get body type
        /// </summary>
        public virtual Microsoft.Ccr.Core.PortSet<LedarrayState,W3C.Soap.Fault> Get()
        {
            Microsoft.Dss.ServiceModel.Dssp.GetRequestType body = new Microsoft.Dss.ServiceModel.Dssp.GetRequestType();
            Get op = new Get(body);
            this.Post(op);
            return op.ResponsePort;

        }
        /// <summary>
        /// Post Get and return the response port.
        /// </summary>
        public virtual Microsoft.Ccr.Core.PortSet<LedarrayState,W3C.Soap.Fault> Get(Microsoft.Dss.ServiceModel.Dssp.GetRequestType body)
        {
            Get op = new Get();
            op.Body = body ?? new Microsoft.Dss.ServiceModel.Dssp.GetRequestType();
            this.Post(op);
            return op.ResponsePort;

        }
    }
    /// <summary>
    /// Ledarray Get Operation
    /// </summary>
    public class Get : Get<GetRequestType, PortSet<LedarrayState, Fault>>
    {
        /// <summary>
        /// Ledarray Get Operation
        /// </summary>
        public Get()
        {
        }
        /// <summary>
        /// Ledarray Get Operation
        /// </summary>
        public Get(Microsoft.Dss.ServiceModel.Dssp.GetRequestType body) : 
                base(body)
        {
        }
        /// <summary>
        /// Ledarray Get Operation
        /// </summary>
        public Get(Microsoft.Dss.ServiceModel.Dssp.GetRequestType body, Microsoft.Ccr.Core.PortSet<LedarrayState,W3C.Soap.Fault> responsePort) : 
                base(body, responsePort)
        {
        }
    }

    /// <summary>
    /// Operation SetSingle: Sets the state of a single LED
    /// </summary>
    public class SetSingle : Update<SetSingleRequest, PortSet<DefaultUpdateResponseType, Fault>>
    {
    }

    /// <summary>
    /// Operation SetBinary: Sets the state of all the LEDs in a vector
    /// </summary>
    public class SetVector : Update<SetVectorRequest, PortSet<DefaultUpdateResponseType, Fault>>
    {
    }

    /// <summary>
    /// Body of the SetSingle message
    /// </summary>
    [DataContract]
    [DataMemberConstructor]
    public class SetSingleRequest
    {
        private int _which;
        private bool _state;

        /// <summary>
        /// Which LED to set (HardwareIdentifier number)
        /// </summary>
        [DataMember]
        public int Which
        {
            get { return this._which; }
            set { this._which = value; }
        }

        /// <summary>
        /// New state of LED
        /// </summary>
        [DataMember]
        public bool State
        {
            get { return this._state; }
            set { this._state = value; }
        }
    }

    /// <summary>
    /// Body of the SetVector message
    /// </summary>
    [DataContract]
    [DataMemberConstructor]
    public class SetVectorRequest
    {
        private int _which;
        private LEDVector _state;

        /// <summary>
        /// Which LED to set (HardwareIdentifier number)
        /// </summary>
        [DataMember]
        public int Which
        {
            get { return this._which; }
            set { this._which = value; }
        }

        /// <summary>
        /// New state of LED
        /// </summary>
        [DataMember]
        public LEDVector State
        {
            get { return this._state; }
            set { this._state = value; }
        }
    }

    [DataContract]
    public class LED
    {
        private int _hardwareIdentifier;
        private bool _state;
        private DateTime _timeStamp;

        /// <summary>
        /// Descriptive identifier number for this sensor
        /// </summary>
        [DataMember]
        [DataMemberConstructor(Order = 1)]
        public int HardwareIdentifier
        {
            get { return this._hardwareIdentifier; }
            set { this._hardwareIdentifier = value; }
        }

        /// <summary>
        /// Last time sensor was updated
        /// </summary>
        [DataMember]
        public DateTime TimeStamp
        {
            get { return this._timeStamp; }
            set { this._timeStamp = value; }
        }

        /// <summary>
        /// The state of the binary sensor
        /// </summary>
        [DataMember]
        public bool State
        {
            get { return this._state; }
            set { this._state = value; }
        }
    }

    [DataContract]
    public class LEDVector
    {
        private List<LED> ledvec;

        [DataMember]
        public List<LED> LEDVec
        {
            get { return ledvec; }
            set { ledvec = value; }
        }
    }
}
