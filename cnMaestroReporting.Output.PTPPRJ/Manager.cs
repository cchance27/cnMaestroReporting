using cnMaestroReporting.cnMaestroAPI.cnDataType;
using cnMaestroReporting.Domain;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace cnMaestroReporting.Output.PTPPRJ
{
    public class Manager
    {
        private Settings _settings { get; } = new Settings();
        private IList<SubscriberRadioInfo> _subscribers { get; }
        private IList<KeyValuePair<string, CnLocation>> _towers { get; }
        private IList<AccessPointRadioInfo> _accessPoints { get; }

        public Manager(
         IConfigurationSection configSection,
         IEnumerable<SubscriberRadioInfo> subscribers,
         IEnumerable<KeyValuePair<string, CnLocation>> towers,
         IEnumerable<AccessPointRadioInfo> accesspoints)
        {
            // Bind our configuration
            configSection.Bind(_settings);

            // Drop subscribers with no latitude/longitude or that we were told are beyond our SM filter distance (assumed invalid)
            _subscribers = subscribers.Where(
                sm => sm.Latitude != 0 && 
                sm.Longitude != 0 && 
                sm.DistanceM < _settings.SmInvalidationRangeM)
                .OrderBy(sm => sm.Name).ToList();

            _towers = towers.OrderBy(tower => tower.Key).ToList();

            _accessPoints = accesspoints.OrderBy(ap => ap.Name).ToList(); // Name sorted dictionary.
        }

        public void Generate()
        {
            XDocument doc = new XDocument(new XDeclaration("1.0", "UTF-8", null));
            XElement LinkPlanner = new XElement("LinkPlanner");
            LinkPlanner.SetAttributeValue("file_version", "2.0");
            LinkPlanner.SetAttributeValue("app_version", "4.9.1");

            XElement MetaData = new XElement("MetaData", "File created with cnMaestroReporting Tool on " + DateTime.Now.ToString());
            LinkPlanner.Add(MetaData);

            XElement Project = new XElement("Project");
            Project.SetAttributeValue("model", "MODEL_ITU");
            Project.SetAttributeValue("default_subscriber_height", _settings.SmHeight.ToString());

            Project.Add(new XElement("Antennas"));
            Project.Add(new XElement("SubscriberAntennas"));

            XElement CustomFields = new XElement("CustomFields");
            XElement CustomFieldSet1 = new XElement("CustomFieldSet");
            CustomFieldSet1.SetAttributeValue("kind", "access_point");
            XElement CustomFieldSet2 = new XElement("CustomFieldSet");
            CustomFieldSet2.SetAttributeValue("kind", "end");
            XElement CustomFieldSet3 = new XElement("CustomFieldSet");
            CustomFieldSet3.SetAttributeValue("kind", "hub");
            XElement CustomFieldSet4 = new XElement("CustomFieldSet");
            CustomFieldSet4.SetAttributeValue("kind", "link");
            XElement CustomFieldSet5 = new XElement("CustomFieldSet");
            CustomFieldSet5.SetAttributeValue("kind", "place");
            XElement CustomFieldSet6 = new XElement("CustomFieldSet");
            CustomFieldSet6.SetAttributeValue("kind", "project");
            XElement CustomFieldSet7 = new XElement("CustomFieldSet");
            CustomFieldSet7.SetAttributeValue("kind", "subscriber");
            XElement CustomFieldSet8 = new XElement("CustomFieldSet");
            CustomFieldSet8.SetAttributeValue("kind", "subscriber_place");
            CustomFields.Add(CustomFieldSet1, CustomFieldSet2, CustomFieldSet3, CustomFieldSet4, CustomFieldSet5, CustomFieldSet6, CustomFieldSet7, CustomFieldSet8);
            Project.Add(CustomFields);

            Project.Add(new XElement("CustomValues"));
            Project.Add(new XElement("Templates"));

            XElement Estimates = new XElement("Estimates");
            Estimates.Add(new XElement("PTPEstimates"));
            Estimates.Add(new XElement("PMPEstimates"));
            Estimates.Add(new XElement("WiFiEstimates"));
            Project.Add(Estimates);

            Project.Add(new XElement("PMPChannelPlans"));

            XElement TddSyncGroups = new XElement("TddSyncGroups");
            XElement TddSyncGroup = new XElement("TddSyncGroup");
            TddSyncGroup.SetAttributeValue("frame_duration", "0");
            TddSyncGroup.SetAttributeValue("max_burst_duration", "0");
            TddSyncGroup.SetAttributeValue("v2", "1");
            TddSyncGroups.Add(TddSyncGroup);
            Project.Add(TddSyncGroups);

            XElement Profiles = new XElement("Profiles");
            Project.Add(Profiles);

            // Generate and add Rules to project
            // We create a single rule for the generic rule and pmp rules since they were the same in a basic project 
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

            Project.Add(CreateXMLRules("Rules", RuleAttribs));
            Project.Add(CreateXMLRules("PMPRules", RuleAttribs));
            
            // Create the rule for AP warnings
            Project.Add(CreateXMLRules("APRule", new RuleAttributeSet
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
            }));

            XElement BestServer = new XElement("BestServer");

            XElement SubscriberVariant = new XElement("SubscriberVariant");
            SubscriberVariant.SetAttributeValue("fade_margin", "0");
            SubscriberVariant.SetAttributeValue("mod_mode", "pmp450_qpsk_mimo_a");
            SubscriberVariant.SetAttributeValue("antenna_height", "10");
            SubscriberVariant.SetAttributeValue("family", "PMP-450");
            BestServer.Add(SubscriberVariant);
            Project.Add(BestServer);

            XElement Places = new XElement("Places");
            var towerCount = 0;
            foreach (KeyValuePair<string, CnLocation> p in _towers)
            {
                XElement Place = new XElement("Place");
                Place.SetAttributeValue("place_id", towerCount.ToString());
                Place.SetAttributeValue("user_hide", "0");
                Place.SetAttributeValue("name", p.Key);
                Place.SetAttributeValue("longitude", p.Value.coordinates[0]);
                Place.SetAttributeValue("shape", "circle");
                Place.SetAttributeValue("latitude", p.Value.coordinates[1]);
                Place.SetAttributeValue("height_asl", "None");
                Place.SetAttributeValue("maximum_height", _settings.TowerHeight);
                towerCount++;
                Places.Add(Place);
            }
            Project.Add(Places);

            var smCount = 0;
            XElement SubscriberPlaces = new XElement("SubscriberPlaces");
            foreach (SubscriberRadioInfo s in _subscribers)
            {
                XElement Place = new XElement("Place");
                Place.SetAttributeValue("place_id", smCount.ToString());
                Place.SetAttributeValue("user_hide", "0");
                Place.SetAttributeValue("name", s.Name);
                Place.SetAttributeValue("longitude", s.Longitude);
                Place.SetAttributeValue("shape", "circle");
                Place.SetAttributeValue("latitude", s.Latitude);
                Place.SetAttributeValue("height_asl", "None");
                Place.SetAttributeValue("maximum_height", _settings.SmHeight.ToString());
                smCount++;
                SubscriberPlaces.Add(Place);
            }
            Project.Add(SubscriberPlaces);

            XElement Links = new XElement("Links");
            Project.Add(Links);

            XElement Hubs = new XElement("Hubs");
            foreach (var t in _towers)
            {
                XElement AccessPoints = new XElement("AccessPoints");
                var thisTowerAps = _accessPoints.Where(a => a.Tower == t.Key).OrderBy(a => a.Name).ToList();
                if (thisTowerAps.Count() > 0)
                {
                    XElement hub = new XElement("Hub");
                    hub.SetAttributeValue("place_id", _towers.IndexOf(t));
                    hub.SetAttributeValue("name", t.Key);

                    foreach (AccessPointRadioInfo a in thisTowerAps)
                    {
                        XElement AccessPoint = new XElement("AccessPoint");
                        AccessPoint.SetAttributeValue("antenna", "64c923a4-8647-4a49-9508-3bee32945d7c"); //TODO class/enum so not fixed to this antenna?
                        AccessPoint.SetAttributeValue("antenna_azimuth", a.Azimuth.ToString());
                        AccessPoint.SetAttributeValue("shape", "triangle");
                        AccessPoint.SetAttributeValue("ap_frequency", a.Channel.ToString());
                        AccessPoint.SetAttributeValue("number", thisTowerAps.IndexOf(a) + 1);

                        XElement Equipment = new XElement("Equipment");
                        Equipment.SetAttributeValue("max_range", _settings.ApRange.ToString()); 
                        Equipment.SetAttributeValue("bandwidth", "20"); //todo this isn't pulled into the typed object yet
                        Equipment.SetAttributeValue("max_range_units", _settings.ApRangeUnits); //todo
                        Equipment.SetAttributeValue("product", "PMP58450i"); //todo ap mising type
                        AccessPoint.Add(Equipment);

                        XElement Subscribers = new XElement("Subscribers");
                        var thisAPSms = _subscribers.Where(x => x.APName == a.Name).ToList();
                        foreach (SubscriberRadioInfo s in thisAPSms)
                        {
                            XElement subscriber = new XElement("Subscriber");
                            subscriber.SetAttributeValue("antenna", "cf7f457e-6015-4021-bc6b-422cfa1f287f");
                            subscriber.SetAttributeValue("place_id", _subscribers.IndexOf(s));
                            subscriber.SetAttributeValue("shape", "rectangle");
                            subscriber.SetAttributeValue("product", "PMP58450i"); //todo device type from model

                            XElement pmplink = new XElement("PMPLink");
                            pmplink.SetAttributeValue("minimum_fade_margin_required_sm", _settings.minimumFadeMarginSM.ToString());
                            pmplink.SetAttributeValue("minimum_fade_margin_required_ap", _settings.minimumFadeMarginAP.ToString());
                            pmplink.SetAttributeValue("required_attribute_sm", "minimum_availability_required_sm");
                            pmplink.SetAttributeValue("required_attribute_ap", "minimum_availability_required_ap");
                            pmplink.SetAttributeValue("minimum_availability_required_sm", _settings.minimumAvailabilitySM.ToString());
                            pmplink.SetAttributeValue("minimum_availability_required_ap", _settings.minimumAvailabilityAP.ToString());
                            subscriber.Add(pmplink);
                            Subscribers.Add(subscriber);
                        }
                        AccessPoint.Add(Subscribers);
                        AccessPoints.Add(AccessPoint);
                    }
                    hub.Add(AccessPoints);
                    Hubs.Add(hub);
                }
            }
            Project.Add(Hubs);

            XElement CustInfo = new XElement("CustInfo");
            Project.Add(CustInfo);

            XElement Description = new XElement("Description");
            Project.Add(Description);

            XElement UI = new XElement("UI");
            XElement Tree = new XElement("Tree");
            Tree.SetAttributeValue("state", "[[0]]");
            UI.Add(Tree);

            XElement SaveLog = new XElement("SaveLog");
            XElement Save = new XElement("Save");

            Save.SetAttributeValue("timestamp", DateTime.Now.ToString("o"));
            Save.SetAttributeValue("hostname", System.Environment.MachineName);
            Save.SetAttributeValue("name", System.Environment.UserName);
            SaveLog.Add(Save);

            LinkPlanner.Add(Project, UI, SaveLog);
            doc.Add(LinkPlanner);

            XmlWriterSettings settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), ConformanceLevel = ConformanceLevel.Document, Indent = true, IndentChars = "  ", };
            String FileName;
            if (String.IsNullOrWhiteSpace(_settings.FileName))
                FileName = $"{DateTime.Now.ToString("yyyy-MM-dd")} - Link Planner.ptpprj";
            else
                FileName = _settings.FileName;

            using (XmlWriter xw = XmlTextWriter.Create(FileName, settings))
            {
                doc.Save(xw);
                xw.Flush();
            }
        }

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

        struct RuleAttributeSet
        {
            public string name;
            public string format;
            public string stop_execution;
            public string disabled;
            public string hidesm;
            public string boolean;
            public string excluded;
            public string hidden;
            public string format_settings;
            public string description;
            public RuleExpressionGroup[] expressionGroups;
        }

        struct RuleExpressionGroup
        {
            public string boolean;
            public RuleExpression[] expressions;
        }

        struct RuleExpression
        {
            public string comparison_value;
            public string predicate;
            public string property;
        }


    }
}