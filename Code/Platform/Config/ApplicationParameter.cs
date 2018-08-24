using System;
using System.Runtime.Serialization;

namespace StockTrader.Platform.Config
{
    [Serializable]    //Set this attribute to all the classes that want to serialize
    
    /// <summary>
    /// Data structure to hold application parameter
    /// </summary>   
    public class ApplicationParameter : ISerializable
    {
        private string _paramProduct;
        private string _paramName;
        private string _paramValue;
        private short _activeStatus;

        /// <summary>
        /// Public constructor of Application Parameter class
        /// </summary>   
        public ApplicationParameter()
        {
            this._paramProduct = string.Empty;
            this._paramName = string.Empty;
            this._paramValue = string.Empty;
        }

        /// <summary>
        /// Product for which this parameter is relevant
        /// </summary>   
        public string ParamProduct
        {
            get { return _paramProduct; }
            set { this._paramProduct = value; }
        }

        /// <summary>
        /// Name of the parameter
        /// </summary>   
        public string ParamName
        {
            get { return _paramName; }
            set { this._paramName = value; }
        }

        /// <summary>
        /// Value of the parameter
        /// </summary>   
        public string ParamValue
        {
            get { return _paramValue; }
            set { this._paramValue = value; }
        }

        /// <summary>
        /// Switch indicating if the parameter is on or off
        /// </summary>   
        public short ActiveStatus
        {
            get { return _activeStatus; }
            set { this._activeStatus = value; }
        }

        /// <summary>
        /// Method to de-serialize the Application Parameter 
        /// </summary>   
        public ApplicationParameter(SerializationInfo info, StreamingContext ctxt)
        {
            ParamProduct = (string)info.GetValue("ParamProduct", typeof(string));
            ParamName = (string)info.GetValue("ParamName", typeof(string));
            ParamValue = (string)info.GetValue("ParamValue", typeof(string));
            ActiveStatus = (short)info.GetValue("ActiveStatus", typeof(short));
        }

        /// <summary>
        /// Method to serialize the current instance of Application Parameter 
        /// </summary>   
        public void GetObjectData(SerializationInfo info, StreamingContext ctxt)
        {
            info.AddValue("ParamProduct", ParamProduct);
            info.AddValue("ParamName", ParamName);
            info.AddValue("ParamValue", ParamValue);
            info.AddValue("ActiveStatus", ActiveStatus);
        }

    }
}
