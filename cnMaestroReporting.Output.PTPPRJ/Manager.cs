using cnMaestroAPI.cnDataType;
using cnMaestroReporting.Domain;
using cnMaestroReporting.Output.PTPPRJ.Rules;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace cnMaestroReporting.Output.PTPPRJ
{
    public partial class Manager
    {
        private Settings settings { get; init; }
        private IList<SubscriberRadioInfo> _subscribers { get; init; }
        private IList<KeyValuePair<string, CnLocation>> _towers { get; }
        private IList<AccessPointRadioInfo> _accessPoints { get; }
        private XElement _project { get; set; }
        private string _fileName { get; init; }

        public Manager(
         IEnumerable<SubscriberRadioInfo> subscribers,
         IEnumerable<KeyValuePair<string, CnLocation>> towers,
         IEnumerable<AccessPointRadioInfo> accesspoints)
        {
            settings = LoadConfiguration();

            ArgumentNullException.ThrowIfNull(settings);

            _fileName = GenerateOutputFilename(settings.FileName);

            _subscribers = FilterSubscribersByInvalidRange(subscribers);

            _towers = towers.OrderBy(tower => tower.Key).ToList();

            _accessPoints = accesspoints.OrderBy(ap => ap.Name).ToList(); // Name sorted dictionary.
        }

        private IList<SubscriberRadioInfo> FilterSubscribersByInvalidRange(IEnumerable<SubscriberRadioInfo> subscribers)
        {
            // Drop subscribers with no latitude/longitude or that we were told are beyond our SM filter distance (assumed invalid)
            return subscribers.Where(
                sm => sm.Latitude != 0 &&
                sm.Longitude != 0 &&
                sm.DistanceGeoM < settings.SmInvalidBeyondRangeM &&
                AirDelayVsGeoDistance(sm.DistanceM, sm.DistanceGeoM, settings.SmDistanceDiffValidM))
                .OrderBy(sm => sm.Name).ToList();
        }

        public bool AirDelayVsGeoDistance(int airdelayM, int geodistanceM, int allowableM)
        {
            var difference = Math.Abs(airdelayM - geodistanceM);
            return difference <= allowableM;
        }

        private string GenerateOutputFilename(string filename)
        {
            // If we have a filename use it if not generate a dated filename.
            if (String.IsNullOrWhiteSpace(filename))
                return $"{DateTime.Now.ToString("yyyy-MM-dd")} - Link Planner.ptpprj";
            
            return settings.FileName;
        }

        private Settings LoadConfiguration()
        {
            var configuration = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
              .Build();

            // Setup config for the main eventloop
            return configuration.GetSection("outputs:ptpprj").Get<Settings>();
        }

        /// <summary>
        /// Generate the XML for the Basic PTPPRJ
        /// </summary>
        public void Generate()
        {
            // Base Project element for storing all project settings.
            _project = new XElement("Project");
            _project.SetAttributeValue("model", "MODEL_ITU");
            _project.SetAttributeValue("default_subscriber_height", settings.SmHeight.ToString());

            // Rule used by the Link and PMP for performance issues
            var RuleAttribs = new RuleAttributeSet
            {
                name = "Does not meet requirements",
                format = "1",
                stop_execution = "0",
                disabled = "0",
                hidesm = "0",
                boolean = "any",
                excluded = "0",
                hidden = "0",
                format_settings = "{\"colour\": \"#ff0000\"}",
                description = "Link fails to meet performance requirements or has warnings",
                expressionGroups = new RuleExpressionGroup[] {
                        new RuleExpressionGroup {
                            boolean = "any" ,
                            expressions = new RuleExpression[] {
                                new RuleExpression {
                                    comparison_value = "False",
                                    predicate ="equal to",
                                    property = "link_ok"
                                }
                            }
                        }
                    }
            };

            // Rule used for the AP if it has warnings
            var APRuleAttribs = new RuleAttributeSet
            {
                name = "Has warnings",
                format = "1",
                stop_execution = "0",
                disabled = "0",
                hidesm = "0",
                boolean = "any",
                excluded = "0",
                hidden = "0",
                format_settings = "{\"colour\": \"#ff000096\", \"text_colour\": \"#ff0000\"}",
                description = "The access point has warnings",
                expressionGroups = new RuleExpressionGroup[] {
                        new RuleExpressionGroup {
                            boolean = "any" ,
                            expressions = new RuleExpression[] {
                                new RuleExpression {
                                    comparison_value = "False",
                                    predicate ="equal to",
                                    property = "is_ok"
                                }
                            }
                        }
                    }
            };

            // Create XML TddSyncGroups 
            XElement TddSyncGroups = new XElement("TddSyncGroups", CreateXMLTddGroup(0, 0, true));
            
            // Setup best server settings (not 100% sure on these but this is default)
            XElement BestServer = new XElement("BestServer", CreateXMLSubVariant(0, "pmp450_qpsk_mimo_a", 10, "PMP-450"));

            // Create XML Entries for all known Towers
            XElement Towers = new XElement("Places", _towers.Select(tower => CreateXMLTowerPlaces(tower)));

            // Create XML Entries for all known Subscribers
            XElement SubscriberPlaces = new XElement("SubscriberPlaces", _subscribers.Select(sm => CreateXMLSubscriberPlaces(sm)));

            // Create XML Hubs for all 
            XElement Hubs = new XElement("Hubs", _towers.Select(tower => CreateXMLHub(tower)));

            // Add elements to project
            _project.Add(
                TddSyncGroups,
                CreateXMLRules("Rules", RuleAttribs),
                CreateXMLRules("PMPRules", RuleAttribs), 
                CreateXMLRules("APRule", APRuleAttribs),
                BestServer,
                Towers,
                SubscriberPlaces,
                Hubs);

        }

        /// <summary>
        /// Save the PTPPRJ to a File
        /// </summary>
        public void Save()
        {
            // Create base LinkPlanner Object
            XElement LinkPlanner = new XElement("LinkPlanner");
            LinkPlanner.SetAttributeValue("file_version", "2.0");
            LinkPlanner.SetAttributeValue("app_version", "4.9.1");
            
            // Required UI Elements for PTPPRJ
            XElement Tree = new XElement("Tree");
            Tree.SetAttributeValue("state", "[[0]]");
            
            // Save information for the file.
            XElement Save = new XElement("Save");
            Save.SetAttributeValue("timestamp", DateTime.Now.ToString("o"));
            Save.SetAttributeValue("hostname", System.Environment.MachineName);
            Save.SetAttributeValue("name", System.Environment.UserName);
            
            // Add necessary elements to the LinkPlanner Layout
            LinkPlanner.Add(
                new XElement("MetaData", "File created with cnMaestroReporting Tool on " + DateTime.Now.ToString()),
                _project,
                new XElement("UI", Tree),
                new XElement("SaveLog", Save)
                );

            // Generate output document and save it to as file.
            XDocument doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), LinkPlanner);
            XmlWriterSettings settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), ConformanceLevel = ConformanceLevel.Document, Indent = true, IndentChars = "  "};
            using (XmlWriter xw = XmlTextWriter.Create(_fileName, settings))
            {
                doc.Save(xw);
                xw.Flush();
            }
        }

        /// <summary>
        /// Generate a ruleset for use in a XML PTPPRJ File
        /// </summary>
        /// <param name="ruleElementName"></param>
        /// <param name="ruleSet"></param>
        /// <returns></returns>
        private XElement CreateXMLRules(string ruleElementName, RuleAttributeSet ruleSet)
        {
            XElement Rules = new XElement(ruleElementName);
            XElement Rule = new XElement("Rule");
            Rule.SetAttributeValue("name", ruleSet.name);
            Rule.SetAttributeValue("format", ruleSet.format);
            Rule.SetAttributeValue("stop_execution", ruleSet.stop_execution);
            Rule.SetAttributeValue("disabled", ruleSet.disabled);
            Rule.SetAttributeValue("hidesm", ruleSet.hidesm);
            Rule.SetAttributeValue("boolean", ruleSet.boolean);
            Rule.SetAttributeValue("excluded", ruleSet.excluded);
            Rule.SetAttributeValue("hidden", ruleSet.hidden);
            Rule.SetAttributeValue("format_settings", ruleSet.format_settings);
            Rule.SetAttributeValue("description", ruleSet.description);
            foreach (RuleExpressionGroup eg in ruleSet.expressionGroups)
            {
                XElement PMPExpressionGroup = new XElement("ExpressionGroup");
                PMPExpressionGroup.SetAttributeValue("boolean", eg.boolean);
                foreach (RuleExpression e in eg.expressions)
                {
                    XElement RuleExpression = new XElement("Expression");
                    RuleExpression.SetAttributeValue("comparison_value", e.comparison_value);
                    RuleExpression.SetAttributeValue("predicate", e.predicate);
                    RuleExpression.SetAttributeValue("property", e.property);
                    PMPExpressionGroup.Add(RuleExpression);
                }
                Rule.Add(PMPExpressionGroup);
            }
            Rules.Add(Rule);
            return Rules;
        }
        
        private XElement CreateXMLTddGroup(float frame_duration, int max_burst_duration, bool v2)
        {
            XElement TddSyncGroup = new XElement("TddSyncGroup");
            TddSyncGroup.SetAttributeValue("frame_duration", frame_duration.ToString());
            TddSyncGroup.SetAttributeValue("max_burst_duration", max_burst_duration.ToString());
            TddSyncGroup.SetAttributeValue("v2", v2 ? "1" : "0");
            return TddSyncGroup;
        }

        /// <summary>
        /// Craete a subscriber variant
        /// </summary>
        /// <returns></returns>
        private XElement CreateXMLSubVariant(int fade_margin, string mod_mode, int antenna_height, string family)
        {
            XElement SubscriberVariant = new XElement("SubscriberVariant");
            SubscriberVariant.SetAttributeValue("fade_margin", fade_margin.ToString());
            SubscriberVariant.SetAttributeValue("mod_mode", mod_mode);
            SubscriberVariant.SetAttributeValue("antenna_height", antenna_height.ToString());
            SubscriberVariant.SetAttributeValue("family", family);
            return SubscriberVariant;
        }

        /// <summary>
        /// Convert towers to XML Places
        /// </summary>
        /// <param name="tower"></param>
        /// <returns></returns>
        private XElement CreateXMLTowerPlaces(KeyValuePair<string, CnLocation> tower)
        {
            XElement Place = new XElement("Place");
            Place.SetAttributeValue("place_id", _towers.IndexOf(tower).ToString());
            Place.SetAttributeValue("name", tower.Key);
            Place.SetAttributeValue("longitude", tower.Value.coordinates[0]);
            Place.SetAttributeValue("shape", "circle");
            Place.SetAttributeValue("latitude", tower.Value.coordinates[1]);
            Place.SetAttributeValue("height_asl", "None");
            Place.SetAttributeValue("maximum_height", settings.TowerHeight);
            return Place;
        }

        /// <summary>
        /// Convert SubscriberRadioInfo to XML Places
        /// </summary>
        /// <param name="sm"></param>
        /// <returns></returns>
        private XElement CreateXMLSubscriberPlaces(SubscriberRadioInfo sm)
        {
            XElement Place = new XElement("Place");
            Place.SetAttributeValue("place_id", _subscribers.IndexOf(sm));
            Place.SetAttributeValue("user_hide", "0");
            Place.SetAttributeValue("name", sm.Name);
            Place.SetAttributeValue("longitude", sm.Longitude);
            Place.SetAttributeValue("shape", "circle");
            Place.SetAttributeValue("latitude", sm.Latitude);
            Place.SetAttributeValue("height_asl", "None");
            Place.SetAttributeValue("maximum_height", settings.SmHeight.ToString());
            return Place;
        }

        /// <summary>
        /// Create an XML Hub with all the AccessPoints and Subscriber connections
        /// </summary>
        /// <param name="tower"></param>
        /// <returns></returns>
        private XElement CreateXMLHub(KeyValuePair<string, CnLocation> tower)
        {
            XElement AccessPoints = new XElement("AccessPoints");
            var thisTowerAps = _accessPoints.Where(a => a.Tower == tower.Key).OrderBy(a => a.Name).ToList();
            if (thisTowerAps.Count() > 0)
            {
                // We've got a tower with a hub so lets start creating the hub
                XElement hub = new XElement("Hub");
                hub.SetAttributeValue("place_id", _towers.IndexOf(tower));
                hub.SetAttributeValue("name", tower.Key);

                foreach (AccessPointRadioInfo a in thisTowerAps)
                {
                    XElement AccessPoint = new XElement("AccessPoint");
                    AccessPoint.SetAttributeValue("antenna_azimuth", a.Azimuth.ToString());
                    AccessPoint.SetAttributeValue("shape", "triangle");
                    AccessPoint.SetAttributeValue("ap_frequency", a.Channel.ToString());
                    AccessPoint.SetAttributeValue("number", thisTowerAps.IndexOf(a) + 1);
                    AccessPoint.SetAttributeValue("frame_period", "2.5");
                    AccessPoint.SetAttributeValue("sm_antenna_height", settings.SmHeight);
                    AccessPoint.SetAttributeValue("modelled_beamwidth", "120.0");


                    XElement Equipment = new XElement("Equipment");
                    Equipment.SetAttributeValue("max_range", settings.ApRange.ToString()); //TODO: pull this from the AP
                    Equipment.SetAttributeValue("bandwidth", "20"); //TODO: pull this from the AP
                    Equipment.SetAttributeValue("max_range_units", settings.ApRangeUnits); //TODO: pull this from the AP
                    Equipment.SetAttributeValue("color_code", a.ColorCode.ToString());
                    Equipment.SetAttributeValue("sync_input", "AutoSync"); // "Generate Sync"
                    Equipment.SetAttributeValue("adjacent_channel_support", "0");
                    Equipment.SetAttributeValue("control_slots", "3"); //TODO

                    Equipment.SetAttributeValue("product", "PMP58450i"); //TODO: calculate this from what we got from the AP
                    AccessPoint.SetAttributeValue("antenna", "64c923a4-8647-4a49-9508-3bee32945d7c"); //TODO class/enum so not fixed to this antenna?
                                                                                                      //8462223a-aa93-4bfb-9023-c3a435fcbfa6 cambium 90
                                                                                                      //64c923a4-8647-4a49-9508-3bee32945d7c cambium 90/120
                                                                                                      //36e8c5ad-b4fa-4e50-a239-7aee4ce3307e cambium 450m
                                                                                                      //AccessPoint.SetAttributeValue("operating_mode", "MEDUSA"); if it's a 450m

                    AccessPoint.Add(Equipment);

                    XElement Subscribers = new XElement("Subscribers");
                    var thisAPSms = _subscribers.Where(x => x.APName == a.Name).ToList();
                    foreach (SubscriberRadioInfo s in thisAPSms)
                    {
                        XElement subscriber = new XElement("Subscriber");
                        subscriber.SetAttributeValue("place_id", _subscribers.IndexOf(s));
                        subscriber.SetAttributeValue("shape", "rectangle");

                        subscriber.SetAttributeValue("product", "PMP58450i"); //todo device type from model
                        subscriber.SetAttributeValue("antenna", "cf7f457e-6015-4021-bc6b-422cfa1f287f"); //TODO: based on product

                        XElement pmplink = new XElement("PMPLink");
                        pmplink.SetAttributeValue("minimum_fade_margin_required_sm", settings.minimumFadeMarginSM.ToString());
                        pmplink.SetAttributeValue("minimum_fade_margin_required_ap", settings.minimumFadeMarginAP.ToString());
                        pmplink.SetAttributeValue("required_attribute_sm", "minimum_availability_required_sm");
                        pmplink.SetAttributeValue("required_attribute_ap", "minimum_availability_required_ap");
                        pmplink.SetAttributeValue("minimum_availability_required_sm", settings.minimumAvailabilitySM.ToString());
                        pmplink.SetAttributeValue("minimum_availability_required_ap", settings.minimumAvailabilityAP.ToString());
                        subscriber.Add(pmplink);
                        Subscribers.Add(subscriber);
                    }
                    AccessPoint.Add(Subscribers);
                    AccessPoints.Add(AccessPoint);
                }
                hub.Add(AccessPoints);

                return hub;
            }
            else
            {
                return null;
            }
        }
    }
}