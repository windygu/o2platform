﻿namespace Amazon.EC2.Model
{
    using System;
    using System.Xml.Serialization;

    [XmlRoot(Namespace="http://ec2.amazonaws.com/doc/2009-11-30/", IsNullable=false)]
    public class DeleteCustomerGatewayRequest
    {
        private string customerGatewayIdField;

        public bool IsSetCustomerGatewayId()
        {
            return (this.customerGatewayIdField != null);
        }

        public DeleteCustomerGatewayRequest WithCustomerGatewayId(string customerGatewayId)
        {
            this.customerGatewayIdField = customerGatewayId;
            return this;
        }

        [XmlElement(ElementName="CustomerGatewayId")]
        public string CustomerGatewayId
        {
            get
            {
                return this.customerGatewayIdField;
            }
            set
            {
                this.customerGatewayIdField = value;
            }
        }
    }
}

