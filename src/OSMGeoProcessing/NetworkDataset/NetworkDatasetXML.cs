using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.XPath;
using ESRI.ArcGIS.Geodatabase;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    /// <summary>
    /// Class to parse OSM Network Dataset Configuration XML
    /// </summary>
    [ComVisible(false)]
    public class NetworkDatasetXML
    {
        private string _configFileName;
        private XElement _xml;

        /// <summary>Constructor - finds, loads and validates config XML</summary>
        public NetworkDatasetXML(string configFile, ResourceManager resMgr)
        {
            try
            {
                _configFileName = configFile;

                if (!File.Exists(_configFileName))
                {
                    throw new ApplicationException(string.Format(
                        resMgr.GetString("GPTools_OSMGPCreateNetworkDataset_noXML"), _configFileName));
                }

                LoadAndValidateXML();
            }
            catch (Exception ex)
            {
                string message = string.Format(resMgr.GetString("GPTools_OSMGPCreateNetworkDataset_xmlParseError"), configFile);
                throw new ApplicationException(message + Environment.NewLine + ex.Message, ex);
            }
        }

        /// <summary>Loads and Validates the XML using embedded XSD</summary>
        private void LoadAndValidateXML()
        {
            const string xsd = "ESRI.ArcGIS.OSM.GeoProcessing.NetworkDataset.NetworkConfig.xsd";

            // Create a schema object from our embedded resource
            Stream streamXSD = Assembly.GetExecutingAssembly().GetManifestResourceStream(xsd);

            XmlSchemaSet schemas = new XmlSchemaSet();
            schemas.Add(string.Empty, XmlReader.Create(streamXSD));

            List<XmlSchemaException> errors = new List<XmlSchemaException>();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.Schemas = schemas;
            settings.IgnoreWhitespace = true;
            settings.ValidationType = ValidationType.Schema;
            settings.ValidationEventHandler += (sender, args) => errors.Add(args.Exception);

            using (StreamReader sr = new StreamReader(_configFileName))
            {
                using (XmlReader reader = XmlReader.Create(sr, settings))
                {
                    XDocument xdoc = XDocument.Load(reader);

                    if (errors.Count > 0)
                    {
                        StringBuilder sbErr = new StringBuilder();
                        sbErr.AppendFormat("Invalid Configuration XML:'{0}'", Path.GetFileName(_configFileName));
                        sbErr.Append(Environment.NewLine);

                        foreach (XmlSchemaException ex in errors)
                        {
                            sbErr.AppendFormat("  Error (line:~{0}, char:{1}): {2}", ex.LineNumber, ex.LinePosition, ex.Message);
                            sbErr.Append(Environment.NewLine);
                        }

                        throw new ApplicationException(sbErr.ToString());
                    }

                    _xml = xdoc.Root;
                }
            }
        }

        /// <summary>Returns sequence of edges from the XML</summary>
        public IEnumerable<SourceFeatureClassInfo> EdgeFeatureClasses()
        {
            return _xml
                .XPathSelectElements("//networkConfiguration/edge")
                .Select(x => new SourceFeatureClassInfo()
                {
                    Name = x.Element("name").Value,
                    SourceType = esriNetworkElementType.esriNETEdge,
                    Query = ((x.Element("query") != null) ? x.Element("query").Value : string.Empty),
                    ConnectPolicy = GetConnectPolicy(x, true),
                    StreetNameFields = GetStreetNameFields(x)
                });
        }

        /// <summary>Returns a unique list of all edge field names from the XML</summary>
        public IEnumerable<string> EdgeFieldNames()
        {
            return _xml
                .XPathSelectElements("//networkConfiguration/edge/osm_fields/osm_field")
                .Select(x => x.Value.ToLower())
                .Distinct();
        }

        /// <summary>Returns sequence of junction classes from the XML</summary>
        public IEnumerable<SourceFeatureClassInfo> JunctionFeatureClasses()
        {
            return _xml
                .XPathSelectElements("//networkConfiguration/junction")
                .Select(x => new SourceFeatureClassInfo()
                {
                    Name = x.Element("name").Value,
                    SourceType = esriNetworkElementType.esriNETJunction,
                    Query = ((x.Element("query") != null) ? x.Element("query").Value : string.Empty),
                    ConnectPolicy = GetConnectPolicy(x, false)
                });
        }

        /// <summary>Returns a unique list of all required junction field names from the XML</summary>
        public IEnumerable<string> JunctionFieldNames()
        {
            return _xml
                .XPathSelectElements("//networkConfiguration/junction/osm_fields/osm_field")
                .Select(x => x.Value.ToLower())
                .Distinct();
        }

        /// <summary>Returns the integer enum value of the edge or junction connection policy in the XML</summary>
        /// <remarks>The XML connect_policy value is assumed to be the ending string of the enumeration name</remarks>
        private int GetConnectPolicy(XElement x, bool isEdge)
        {
            const int edgeDefault = (int)esriNetworkEdgeConnectivityPolicy.esriNECPAnyVertex;
            const int jnctDefault = (int)esriNetworkJunctionConnectivityPolicy.esriNJCPHonor;

            XElement xConnect = x.Element("connect_policy");
            if (xConnect != null)
            {
                string policy = xConnect.Value;
                if (isEdge)
                {
                    foreach (esriNetworkEdgeConnectivityPolicy cp in Enum.GetValues(typeof(esriNetworkEdgeConnectivityPolicy)))
                    {
                        if (cp.ToString().EndsWith(policy, StringComparison.CurrentCultureIgnoreCase))
                            return (int)cp;
                    }
                }
                else
                {
                    foreach (esriNetworkJunctionConnectivityPolicy cp in Enum.GetValues(typeof(esriNetworkJunctionConnectivityPolicy)))
                    {
                        if (cp.ToString().EndsWith(policy, StringComparison.CurrentCultureIgnoreCase))
                            return (int)cp;
                    }
                }
            }

            return ((isEdge) ? edgeDefault : jnctDefault);
        }

        /// <summary>Returns a StreetNameFields object gleaned from the given XElement</summary>
        private StreetNameFields GetStreetNameFields(XElement x)
        {
            StreetNameFields snf = new StreetNameFields();

            if (x != null)
            {
                XElement xSNF = x.Element("street_name_fields");
                if (xSNF != null)
                {
                    snf.DirectionPrefix = (string)xSNF.Element("direction_prefix");
                    snf.TypePrefix = (string)xSNF.Element("type_prefix");
                    snf.StreetName = (string)xSNF.Element("street_name");
                    snf.TypeSuffix = (string)xSNF.Element("type_suffix");
                    snf.DirectionSuffix = (string)xSNF.Element("direction_suffix");

                    if (string.IsNullOrEmpty(snf.StreetName))
                        throw new ApplicationException("XML Element: 'street_name' must be given when specifying edge source directions");
                }
            }

            return snf;
        }

        /// <summary>Returns the turn feature class name defined in the XML</summary>
        public string TurnFeatureClassName()
        {
            XElement xTurn = _xml.XPathSelectElement("//networkConfiguration/turn");
            return ((xTurn != null) ? xTurn.Value : null);
        }

        /// <summary>Returns sequence of connectivity groups defined in the XML</summary>
        public IEnumerable<IList<string>> ConnectivityGroups()
        {
            foreach (XElement xGroup in _xml.XPathSelectElements("//networkConfiguration/connectivity/group"))
            {
                yield return xGroup.Elements("source").Select(src => src.Value).ToList();
            }
        }

        /// <summary>Returns general network direction information from the XML</summary>
        public GeneralNetworkDirectionInfo GeneralNetworkDirections()
        {
            XElement xDirections = _xml.XPathSelectElement("//networkConfiguration/directions");
            if (xDirections == null)
                return null;

            GeneralNetworkDirectionInfo dirInfo = new GeneralNetworkDirectionInfo();
            dirInfo.LengthAttr = (string)xDirections.Element("length_attr");

            XElement xUnits = xDirections.Element("length_units");
            if (xUnits != null)
                dirInfo.LengthUnits = (esriNetworkAttributeUnits)Enum.Parse(typeof(esriNetworkAttributeUnits), "esriNAU" + xUnits.Value);

            dirInfo.RoadClassAttr = (string)xDirections.Element("road_class_attr");
            dirInfo.TimeAttr = (string)xDirections.Element("time_attr");

            return dirInfo;
        }

        /// <summary>Returns sequence of network attributes defined in the XML</summary>
        public IEnumerable<NetworkAttributeInfo> NetworkAttributes()
        {
            return _xml
                .XPathSelectElements("//networkConfiguration/network/network_attribute")
                .Select(x => CreateNetworkAttributeInfo(x));
        }

        /// <summary>Creates a single NetworkAttributeInfo for the given network_attribute element</summary>
        private NetworkAttributeInfo CreateNetworkAttributeInfo(XElement x)
        {
            NetworkAttributeInfo netAttrInfo = new NetworkAttributeInfo();
            netAttrInfo.Name = x.Element("name").Value;
            netAttrInfo.UsageType = GetNetworkAttributeUsageType(x);

            XElement xType = null;
            switch (netAttrInfo.UsageType)
            {
                case esriNetworkAttributeUsageType.esriNAUTCost:
                    xType = x.Element("cost");
                    netAttrInfo.DataType = GetNetworkAttributeDataType(xType);
                    netAttrInfo.UseAsDefault = bool.Parse(xType.Attribute("useAsDefault").Value);
                    netAttrInfo.Units = GetNetworkAttributeUnits(xType);
                    netAttrInfo.Parameters = GetNetworkAttributeParameters(xType).ToList();
                    netAttrInfo.Evaluators = GetNetworkAttributeEvaluators(xType).ToList();
                    break;

                case esriNetworkAttributeUsageType.esriNAUTHierarchy:
                    xType = x.Element("hierachy");
                    netAttrInfo.DataType = esriNetworkAttributeDataType.esriNADTInteger;
                    netAttrInfo.UseAsDefault = bool.Parse(xType.Attribute("useAsDefault").Value);
                    netAttrInfo.Units = esriNetworkAttributeUnits.esriNAUUnknown;
                    netAttrInfo.Parameters = GetNetworkAttributeParameters(xType).ToList();
                    netAttrInfo.Evaluators = GetNetworkAttributeEvaluators(xType).ToList();
                    break;

                case esriNetworkAttributeUsageType.esriNAUTRestriction:
                    xType = x.Element("restriction");
                    netAttrInfo.DataType = esriNetworkAttributeDataType.esriNADTBoolean;
                    netAttrInfo.UseAsDefault = bool.Parse(xType.Attribute("useAsDefault").Value);
                    netAttrInfo.Units = esriNetworkAttributeUnits.esriNAUUnknown;
                    netAttrInfo.Parameters = GetNetworkAttributeParameters(xType).ToList();
                    netAttrInfo.Evaluators = GetNetworkAttributeEvaluators(xType).ToList();
                    break;

                case esriNetworkAttributeUsageType.esriNAUTDescriptor:
                    xType = x.Element("descriptor");
                    netAttrInfo.DataType = GetNetworkAttributeDataType(xType);
                    netAttrInfo.UseAsDefault = false;
                    netAttrInfo.Units = esriNetworkAttributeUnits.esriNAUUnknown;
                    netAttrInfo.Parameters = GetNetworkAttributeParameters(xType).ToList();
                    netAttrInfo.Evaluators = GetNetworkAttributeEvaluators(xType).ToList();
                    break;
            }

            netAttrInfo.DefaultValue = GetNetworkAttributeDefaultValue(netAttrInfo.DataType, x.Element("default_value").Value);

            return netAttrInfo;
        }

        /// <summary>Retrieves ParameterInfo objects from the given XML element</summary>
        private IEnumerable<ParameterInfo> GetNetworkAttributeParameters(XElement x)
        {
            foreach (XElement xParam in x.Elements("parameter"))
            {
                ParameterInfo paramInfo = new ParameterInfo();
                paramInfo.Name = xParam.Element("name").Value;
                paramInfo.DataType = GetNetworkAttributeDataType(xParam);
                paramInfo.DefaultValue = GetNetworkAttributeDefaultValue(paramInfo.DataType, xParam.Element("default_value").Value);

                yield return paramInfo;
            }
        }

        /// <summary>Retrieves EvaluatorInfo objects from the given XML element</summary>
        private IEnumerable<EvaluatorInfo> GetNetworkAttributeEvaluators(XElement x)
        {
            foreach (XElement xEval in x.Elements("evaluator_attributes"))
            {
                EvaluatorInfo evalInfo = new EvaluatorInfo();
                evalInfo.Source = xEval.Element("source").Value;

                if (xEval.Element("direction").Value.Equals("From-To"))
                    evalInfo.Direction = esriNetworkEdgeDirection.esriNEDAlongDigitized;
                else
                    evalInfo.Direction = esriNetworkEdgeDirection.esriNEDAgainstDigitized;

                XElement xConstant = xEval.Element("Constant");
                XElement xField = xEval.Element("Field");
                XElement xScript = xEval.Element("Script");

                if (xConstant != null)
                {
                    evalInfo.EvaluatorType = eEvaluatorType.Constant;
                    evalInfo.Expression = xConstant.Element("value").Value;
                }
                else if (xField != null)
                {
                    evalInfo.EvaluatorType = eEvaluatorType.Field;
                    evalInfo.ScriptLanguage = xField.Attribute("script_type").Value;
                    evalInfo.Expression = xField.Element("expression").Value;
                    evalInfo.Prelogic = GetExprPreLogic(xField);
                }
                else if (xScript != null)
                {
                    evalInfo.EvaluatorType = eEvaluatorType.Script;
                    evalInfo.ScriptLanguage = xScript.Attribute("script_type").Value;
                    evalInfo.Expression = xScript.Element("expression").Value;
                    evalInfo.Prelogic = GetExprPreLogic(xScript);
                }

                yield return evalInfo;
            }
        }

        /// <summary>Retrieves esriNetworkAttributeUsageType element from the given XML element</summary>
        private esriNetworkAttributeUsageType GetNetworkAttributeUsageType(XElement x)
        {
            if (x.Element("cost") != null)
                return esriNetworkAttributeUsageType.esriNAUTCost;
            else if (x.Element("hierachy") != null)
                return esriNetworkAttributeUsageType.esriNAUTHierarchy;
            else if (x.Element("restriction") != null)
                return esriNetworkAttributeUsageType.esriNAUTRestriction;
            else
                return esriNetworkAttributeUsageType.esriNAUTDescriptor;
        }

        /// <summary>Retrieves datatype element from the given XML element</summary>
        private esriNetworkAttributeDataType GetNetworkAttributeDataType(XElement x)
        {
            XElement xDataType = x.Element("datatype");
            if (xDataType != null)
            {
                if (xDataType.Value.Equals("integer", StringComparison.CurrentCultureIgnoreCase))
                    return esriNetworkAttributeDataType.esriNADTInteger;
                else if (xDataType.Value.Equals("float", StringComparison.CurrentCultureIgnoreCase))
                    return esriNetworkAttributeDataType.esriNADTFloat;
                else if (xDataType.Value.Equals("double", StringComparison.CurrentCultureIgnoreCase))
                    return esriNetworkAttributeDataType.esriNADTDouble;
                else if (xDataType.Value.Equals("boolean", StringComparison.CurrentCultureIgnoreCase))
                    return esriNetworkAttributeDataType.esriNADTBoolean;
            }
            else
            {
                if (x.Name.Equals("restriction"))
                    return esriNetworkAttributeDataType.esriNADTBoolean;

                if (x.Name.Equals("hierachy"))
                    return esriNetworkAttributeDataType.esriNADTInteger;
            }

            return esriNetworkAttributeDataType.esriNADTInteger;
        }

        /// <summary>Retrieves units element from the given XML element</summary>
        private esriNetworkAttributeUnits GetNetworkAttributeUnits(XElement x)
        {
            XElement xUnits = x.Element("units");
            if (xUnits != null)
                return (esriNetworkAttributeUnits)Enum.Parse(typeof(esriNetworkAttributeUnits), "esriNAU" + xUnits.Value);

            return esriNetworkAttributeUnits.esriNAUUnknown;
        }

        /// <summary>Converts a given string to a esriNetworkAttributeDataType base type</summary>
        private object GetNetworkAttributeDefaultValue(esriNetworkAttributeDataType datatype, string value)
        {
            if (datatype == esriNetworkAttributeDataType.esriNADTBoolean)
                return TypeConvert<bool>(value);
            else if (datatype == esriNetworkAttributeDataType.esriNADTDouble)
                return TypeConvert<double>(value);
            else if (datatype == esriNetworkAttributeDataType.esriNADTFloat)
                return TypeConvert<float>(value);
            else if (datatype == esriNetworkAttributeDataType.esriNADTInteger)
                return TypeConvert<int>(value);

            return null;
        }

        /// <summary>Converts a string value to a base type</summary>
        private static object TypeConvert<T>(string value)
        {
            T result;

            try
            {
                result = (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                result = default(T);
            }

            return result;
        }

        /// <summary>Gets prelogic expression from XML</summary>
        private string GetExprPreLogic(XElement x)
        {
            XElement xPrelogic = x.Element("pre_logic");
            if (xPrelogic == null)
                return string.Empty;

            var lines = xPrelogic.Value.Split('\n')
                .Select(line => line.Replace('\t', ' '))
                .Select(line => line.TrimEnd())
                .SkipWhile(line => line.Length == 0);

            int leadingSpaceCount = lines.First().TakeWhile(ch => (ch == ' ')).Count();
            var trimmedLines = lines.Select(line => TrimLeadingSpaces(line, leadingSpaceCount));

            return string.Join("\n", trimmedLines.ToArray());
        }

        // Trims leading spaces from the given string to the given max number of spaces
        private string TrimLeadingSpaces(string str, int max)
        {
            int count = 0;
            foreach (char c in str)
            {
                if (c == ' ')
                {
                    ++count;
                    if (count == max)
                        break;
                }
                else
                    break;
            }

            return str.Substring(count);
        }
    }

    [ComVisible(false)]
    public class SourceFeatureClassInfo
    {
        public esriNetworkElementType SourceType { get; set; }
        public string Name { get; set; }
        public string Query { get; set; }
        public int ConnectPolicy { get; set; }
        public StreetNameFields StreetNameFields { get; set; }
    }

    [ComVisible(false)]
    public class StreetNameFields
    {
        public string DirectionPrefix { get; set; }
        public string TypePrefix { get; set; }
        public string StreetName { get; set; }
        public string TypeSuffix { get; set; }
        public string DirectionSuffix { get; set; }
    }

    [ComVisible(false)]
    public class GeneralNetworkDirectionInfo
    {
        public string LengthAttr { get; set; }
        public esriNetworkAttributeUnits LengthUnits { get; set; }
        public string RoadClassAttr { get; set; }
        public string TimeAttr { get; set; }
    }

    [ComVisible(false)]
    public class NetworkAttributeInfo
    {
        public string Name { get; set; }
        public esriNetworkAttributeUsageType UsageType { get; set; }
        public esriNetworkAttributeDataType DataType { get; set; }
        public esriNetworkAttributeUnits Units { get; set; }
        public bool UseAsDefault { get; set; }
        public object DefaultValue { get; set; }
        public List<EvaluatorInfo> Evaluators { get; set; }
        public List<ParameterInfo> Parameters { get; set; }
    }

    [ComVisible(false)]
    public enum eEvaluatorType { Constant = 0, Field, Script };

    [ComVisible(false)]
    public class EvaluatorInfo
    {
        public string Source { get; set; }
        public esriNetworkEdgeDirection Direction { get; set; }
        public eEvaluatorType EvaluatorType { get; set; }
        public string ScriptLanguage { get; set; }
        public string Prelogic { get; set; }
        public string Expression { get; set; }
    }

    [ComVisible(false)]
    public class ParameterInfo
    {
        public string Name { get; set; }
        public esriNetworkAttributeDataType DataType { get; set; }
        public object DefaultValue { get; set; }
    }
}
