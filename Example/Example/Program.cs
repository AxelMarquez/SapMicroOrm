using SAP.Middleware.Connector;
using SapMicroOrm;
using System.Collections.Generic;
using System.Linq;

namespace Example
{
    class Program
    {
        static void Main(string[] args)
        {
            var sapConn = RfcDestinationManager.GetDestination(BuildConfigParameters(name: "AMQ", language: "EN", username: "uid38981", password: "YOUR PASSWORD HERE", serverHost: "amqusr.f1.de.conti.de", systemNumber: "35", client: "300"));

            List<MARA> materialHeaders = sapConn
                .From<MARA>()
                .Where("MATNR = '000000000000033916'")
                .SelectAllColumns();

            List<Material> materials = sapConn
                .From<Material>()
                .Where(materialHeaders.Select(mh => $"MATNR = '{mh.MATNR}'")) // <- Join here
                .SelectAllColumns();
        }

        public static RfcConfigParameters BuildConfigParameters(string name, string language, string username, string password, string serverHost, string systemNumber, string client)
        {
            RfcConfigParameters parameters = new RfcConfigParameters();
            parameters[RfcConfigParameters.Name] = name;
            parameters[RfcConfigParameters.Language] = language;
            parameters[RfcConfigParameters.User] = username;
            parameters[RfcConfigParameters.Password] = password;
            parameters[RfcConfigParameters.AppServerHost] = serverHost;
            parameters[RfcConfigParameters.SystemNumber] = systemNumber;
            parameters[RfcConfigParameters.Client] = client;
            parameters[RfcConfigParameters.PeakConnectionsLimit] = "999";
            return parameters;
        }

        public class MARA
        {
            public string MATNR { get; set; }
            public string MTART { get; set; }
            public string MATKL { get; set; }
            //Omitted the rest of the columns for simplicity
        }

        [Alias("MARC")]
        public class Material
        {
            [Alias("MATNR")]
            public string Id { get; set; }

            [Alias("WERKS")]
            public string Plant { get; set; }
            //Omitted the rest of the columns for simplicity
        }
    }
}
