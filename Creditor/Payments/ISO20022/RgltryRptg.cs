using System;
using System.Collections.Generic;
using System.Xml;
using Uniconta.DataModel;

namespace UnicontaISO20022CreditTransfer
{
    public class RgltryRptg
    {
        private readonly ExportFormatType exportFormat;
        private readonly ISO20022PaymentTypes ISOPaymType;
        private readonly int code;
        private string text;

        private const string RGLTRYRPTG = "RgltryRptg";
        private const string DTLS = "Dtls";
        private const string CD = "Cd";
        private const string INF = "Inf";

        public RgltryRptg(byte exportFormat, ISO20022PaymentTypes ISOPaymType, int code, string text)
        {
            this.code = code;
            this.text = text;
            this.exportFormat = (ExportFormatType)exportFormat;
            this.ISOPaymType = ISOPaymType;
        }

        internal virtual void Append(BaseDocument baseDoc, XmlDocument doc, XmlElement parent)
        {
            if (code > 0 && (exportFormat == ExportFormatType.ISO20022_NO || exportFormat == ExportFormatType.ISO20022_SE) && (ISOPaymType == ISO20022PaymentTypes.CROSSBORDER || ISOPaymType == ISO20022PaymentTypes.SEPA))
            {
                XmlElement RgltryRptg = baseDoc.AppendElement(doc, parent, RGLTRYRPTG);
                XmlElement Dtls = baseDoc.AppendElement(doc, RgltryRptg, DTLS);

                baseDoc.AppendElement(doc, Dtls, CD, code.ToString());
                if (text != null)
                {
                    if (text.Length > 35)
                        text = text.Substring(0, 35);

                    baseDoc.AppendElement(doc, Dtls, INF, text);
                }
            }
        }
    }
}
