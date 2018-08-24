using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Xml;
//using StockTrader.Platform.Database.Platform.Utilities.Commerce.PaymentProviders.PayPal;
//using StockTrader.Platform.Database.Platform.Utilities.Database;
//using StockTrader.Platform.Database.Platform.Utilities.RulesEngine;
//using StockTrader.Platform.Database.Platform.Utilities.Support;
//using StockTrader.Platform.Database.Platform.Utilities.UrlAnalysis.DataContainers;
//using StockTrader.Platform.Database.Platform.Utilities.Utilities;
//using StockTrader.Platform.Database.Platform.Utilities.WebQueryLibrary.Alexa;
//using StockTrader.Platform.Database.Platform.Utilities.Workflow;
//using StockTrader.Platform.Database.Platform.Utilities.Users;
//using Extreme.Mathematics.SpecialFunctions;
using System.IO;
using System.ComponentModel;

namespace StockTrader.Platform.Config
{
    /// <summary>
    /// Application config loads all the config definitions required to run different products in the suite
    /// </summary>  
    public static class ApplicationConfig
    {
        /// <summary>
        /// Static constructor of ApplicationConfig to do the startup stuff (init vars, get config params etc.)
        /// </summary>
        static ApplicationConfig()
        {
            try
            {
                _ticketsProcessorPeriodInMinutes = int.Parse(ConfigurationManager.AppSettings["TicketsProcessorPeriodInMinutes"]);
            }
            catch (Exception ex)
            {
                _ticketsProcessorPeriodInMinutes = 15; // Default 15 minutes
                Trace.TraceWarning(DateTime.Now + " : " + ex.Message);
                Trace.TraceWarning(DateTime.Now + " : " + "TicketsProcessorPeriodInMinutes Value not found: Setting default time to " 
                    + _ticketsProcessorPeriodInMinutes + " minutes");
            }
            try
            {
                bRunTicketsEmailProcessor = bool.Parse(ConfigurationManager.AppSettings["RunTicketsEmailProcessor"]);
                Trace.TraceInformation(DateTime.Now + " : " + "TicketsEmailProcessor is {0}", bRunTicketsEmailProcessor ? "ON" : "OFF");
            }
            catch (Exception ex)
            {
                _ticketsProcessorPeriodInMinutes = 15; // Default 15 minutes
                // If we failed getting config parameters, trace the error for log analysis
                Trace.TraceWarning(DateTime.Now + " : " + ex.Message);
                Trace.TraceWarning(DateTime.Now + " : " + "RunTicketsEmailProcessor Switch not found: Email Processing is turned OFF");
            }

            try
            {
                bRunPaypalReconciler = bool.Parse(ConfigurationManager.AppSettings["RunPaypalReconciler"]);
                Trace.TraceInformation(DateTime.Now + " : " + "PaypalReconciler is {0}", bRunPaypalReconciler ? "ON" : "OFF");
            }
            catch (Exception ex)
            {
                // If we failed getting config parameters, trace the error for log analysis
                Trace.TraceWarning(DateTime.Now + " : " + ex.Message);
                Trace.TraceWarning(DateTime.Now + " : " + "RunPaypalReconciler Switch not found: Paypal reconciliation is turned OFF");
            }

            try
            {
                bRunWorkflowManagers = bool.Parse(ConfigurationManager.AppSettings["RunWorkflowManagers"]);
                Trace.TraceInformation(DateTime.Now + " : " + "RunWorkflowManagers is {0}", bRunWorkflowManagers ? "ON" : "OFF");
            }
            catch (Exception ex)
            {
                // If we failed getting config parameters, trace the error for log analysis
                Trace.TraceWarning(DateTime.Now + " : " + ex.Message);
                Trace.TraceWarning(DateTime.Now + " : " + "RunWorkflowManagers Switch not found: WorkflowManagers are turned OFF");
            }
        }

        private static List<HashSet<string>>  _urlCatagory=new List<HashSet<string>>(4);
        private static string _serverHost = string.Empty;
        private static string _physicalFilePath = string.Empty;
        private static HashSet<string> _stopWords = new HashSet<string>();
        private static HashSet<string> _domainSuffixTokens = new HashSet<string>();
        private static HashSet<string> _inheritableCssProperties = new HashSet<string>();
        private static HashSet<string> _excludeHostsFromNormalization = new HashSet<string>();
        private static HashSet<string> _badHosts = new HashSet<string>();
        private static Hashtable _tagDefinitions = new Hashtable(100);
        private static List<RuleDefinition> _ruleBook = new List<RuleDefinition>(20);
        private static Hashtable _applParams = new Hashtable(50);
        private static Hashtable _filteredUrls = new Hashtable(50);
        private static Hashtable _emailConfiguration = new Hashtable(20);
        private static string _defaultCssStyles = string.Empty;
        private static Dictionary<string, AlexaResponse> _alexaDetails = new Dictionary<string, AlexaResponse>(1200);
        private static Dictionary<string, DateTime> _urlRequestTimes = new Dictionary<string, DateTime>(500);
        private static double[] _fibCache = new double[30];
        private static double[] _queryWeightLookup = new double[5];

        private static Dictionary<string, double> _zoneAttributePropertiesLookup = new Dictionary<string, double>(150);


        private static List<string> _zones = new List<string>(40);


        private static ReaderWriterLockSlim _alexaDetailsLock = new ReaderWriterLockSlim();
        private static ReaderWriterLockSlim _badHostsLock = new ReaderWriterLockSlim();
        private static ReaderWriterLockSlim _urlRequestTimesLock = new ReaderWriterLockSlim();

        private static ThreadStart _alexaThreadJob = new ThreadStart(DoAlexaDataLoad);
        public static Thread _alexaThread = new Thread(_alexaThreadJob);
        static bool _isAlexaActive = false;

        // Workflow
        private static readonly bool bRunWorkflowManagers;
        private static WorkPool _workPool = new WorkPool();
        private static short _numJobsToPick = 1;
        private static WorkflowManager _workFlowManager = new WorkflowManager(_workPool, 1);
        private static ThreadStart _wfmStart = new ThreadStart(_workFlowManager.StartManager);
        private static Thread _wfmThread = new Thread(_wfmStart);
        
        // TicketsEmailProcessor
        private static Timer _ticketsEmailProcessor;
        private static readonly bool bRunTicketsEmailProcessor;
        private static int _ticketsProcessorPeriodInMinutes;

        static BackgroundWorker _bwTicketsProcessor = new BackgroundWorker
        {
            WorkerSupportsCancellation = true
        };

        // Payment Reconciler (Paypal)
        private static PayPalReconciler _paypalReconciler = new PayPalReconciler();
        private static Thread _paypalThread = new Thread(new ThreadStart(_paypalReconciler.Start));
        private static readonly bool bRunPaypalReconciler;

        /// <summary>
        /// App Instance identifier (Unique Id for this instance of the app)
        /// TODO: Create a mechanism to issue these Ids in multiple App Instance (eg. Web Farm) scenario.
        /// </summary>
        public static readonly string AppInstanceId = ConfigurationManager.AppSettings["ServerHost"];



        public static List<HashSet<string>> UrlCatagory 
        {
            set { _urlCatagory = value; }
            get { return _urlCatagory; }
        }



        /// <summary>
        /// Server Host of the application
        /// </summary>
        public static readonly string ServerHost = ConfigurationManager.AppSettings["ServerHost"];

        public static Dictionary<string, double> ZoneAttributePropertiesLookup
        {
            set { _zoneAttributePropertiesLookup = value; }
            get { return _zoneAttributePropertiesLookup; }
        }

        public static List<string> Zones
        {
            set { _zones = value; }
            get { return _zones; }
        }

        public static short NumJobsToPick
        {
            set { _numJobsToPick = value; }
            get { return _numJobsToPick; }
        }

        /// <summary>
        /// Physical path of the virtual directory
        /// </summary>
        public static string PhysicalFilePath
        {
            set { _physicalFilePath = value; }
            get { return _physicalFilePath; }
        }

        /// <summary>
        /// Hashtable containing tag configuration definitions 
        /// </summary>  
        public static Hashtable TagDefinitions
        {
            get { return _tagDefinitions; }
        }

        /// <summary>
        /// List containing all the rules/alerts configured within the system
        /// </summary>  
        public static List<RuleDefinition> RuleBook
        {
            get { return _ruleBook; }
        }

        /// <summary>
        /// List of stop words in English
        /// Will need to enhance this to load stop words of other languages
        /// </summary>  
        public static HashSet<string> StopWordsList
        {
            get { return _stopWords; }
        }

        /// <summary>
        /// Domain suffix tokens
        /// </summary>  
        public static HashSet<string> DomainSuffixTokens
        {
            get { return _domainSuffixTokens; }
        }

        /// <summary>
        /// Inheritable Css Properties
        /// </summary>  
        public static HashSet<string> InheritableCssProperties
        {
            get { return _inheritableCssProperties; }
        }

        /// <summary>
        /// Hosts to exclude from Normalization
        /// </summary>  
        public static HashSet<string> ExcludeHostsFromNormalization
        {
            get { return _excludeHostsFromNormalization; }
        }

        /// <summary>
        /// Hosts to exclude from Normalization
        /// </summary>  
        public static HashSet<string> BadHosts
        {
            get { return _badHosts; }
        }

        /// <summary>
        /// List of global application parameters
        /// </summary>  
        public static Hashtable ApplParams
        {
            get { return _applParams; }
        }

        /// <summary>
        /// Table of global to-be filtered urls (malicious urls causing outof memory etc. incl. ones we cannot process due to other reasons as well)
        /// </summary>  
        public static Hashtable FilteredUrls
        {
            get { return _filteredUrls; }
        }

        /// <summary>
        /// Table of global outgoing email parameters
        /// </summary>  
        public static Hashtable EmailConfiguration
        {
            get { return _emailConfiguration; }
        }

        /// <summary>
        /// Default CSS styles used in HTML 4.0 specification
        /// </summary>  
        public static string DefaultCssStyles
        {
            get { return _defaultCssStyles; }
        }

        /// <summary>
        /// Alexa details
        /// </summary>
        public static Dictionary<string, AlexaResponse> AlexaDetails
        {
            get { return _alexaDetails; }
        }

        /// <summary>
        /// Dictionary that holds UrlRequestTimes
        /// </summary>
        public static Dictionary<string, DateTime> UrlRequestTimes
        {
            get { return _urlRequestTimes; }
        }

        /// <summary>
        /// Array that holds fibonacchi values
        /// example:  index 0 will have fibnochhi value for 1(1.0), index 1 will have fibnochhi value for 2(1.0), index 2 will have fibnochhi value for 3 (2.0) and so on
        /// </summary>
        public static double[] FibValuesCache
        {
            get { return _fibCache; }
        }

        /// <summary>
        /// Lookup that holds Math.Pow(2, 8 * numberOfSalientWords)
        /// </summary>
        public static double[] QueryWeightLookup
        {
            get { return _queryWeightLookup; }
        }


        public static void LoadUrlCatagory() 
        {
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/DirectorySubmissionDomains.xml");

            _stopWords.Clear();
            for(int i=0;i<4;i++)
            _urlCatagory.Add(new HashSet<string>());

            foreach (XmlNode domain in xDoc.GetElementsByTagName("domain"))
            {
                if (!_urlCatagory[0].Contains(domain.InnerText))
                {

                    _urlCatagory[0].Add(domain.InnerText);
                }
            }
            xDoc = new XmlDocument();
            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/ArticleMarketingDomains.xml");

            _stopWords.Clear();

            foreach (XmlNode domain in xDoc.GetElementsByTagName("domain"))
            {

                if (!_urlCatagory[1].Contains(domain.InnerText))
                {

                    _urlCatagory[1].Add(domain.InnerText);
                }
            }
            xDoc = new XmlDocument();
            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/PressReleaseDomains.xml");

            _stopWords.Clear();

            foreach (XmlNode domain in xDoc.GetElementsByTagName("domain"))
            {

                if (!_urlCatagory[2].Contains(domain.InnerText))
                {

                    _urlCatagory[2].Add(domain.InnerText);
                }
            }

            xDoc = new XmlDocument();
            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/SocialMediaDomains.xml");

            _stopWords.Clear();

            foreach (XmlNode domain in xDoc.GetElementsByTagName("domain"))
            {

                if (!_urlCatagory[3].Contains(domain.InnerText))
                {

                    _urlCatagory[3].Add(domain.InnerText);
                }
            }

        }


        public static void LoadWeights()
        {
            XmlDocument xDoc = new XmlDocument();

            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/AlgorithmData.xml");

            Func<XmlElement, string, string> helperFuntion = (element, tag) =>
            {
                XmlNodeList nodes = element.GetElementsByTagName(tag);
                if (nodes != null && nodes.Count > 0)
                    return nodes[0].InnerText;
                else
                    return String.Empty;
            };


            foreach (XmlElement zoneData in xDoc.GetElementsByTagName("ZoneData"))
            {
                try
                {
                    string zoneName = helperFuntion(zoneData, "ZoneName");
                    if (!_zones.Contains(zoneName))
                        _zones.Add(zoneName);
                    int typeOfAnalysis = int.Parse(helperFuntion(zoneData, "TypeOfAnalysis"));
                    int typeOfWordScore = int.Parse(helperFuntion(zoneData, "TypeOfWordScore"));

                    if (typeOfWordScore == 0)
                    {
                        //Read Zone Weights
                        string zoneWeightText = helperFuntion(zoneData, "ZoneWeight");
                        if (!zoneWeightText.Equals(string.Empty))
                        {
                            string key = typeOfAnalysis.ToString() + "#" + zoneName + "#" + "W";
                            _zoneAttributePropertiesLookup.Add(key, double.Parse(zoneWeightText));
                        }

                        string spamThresholdText = helperFuntion(zoneData, "SpamThreshold");
                        if (!spamThresholdText.Equals(string.Empty))
                        {
                            string key = typeOfAnalysis.ToString() + "#" + zoneName + "#" + "S";
                            _zoneAttributePropertiesLookup.Add(key, double.Parse(spamThresholdText));
                        }


                        string contextualWeightText = helperFuntion(zoneData, "ContextualWeight");
                        if (!contextualWeightText.Equals(string.Empty))
                        {
                            string key = typeOfAnalysis.ToString() + "#" + zoneName + "#" + "C";
                            _zoneAttributePropertiesLookup.Add(key, double.Parse(contextualWeightText));
                        }

                        zoneWeightText = null;
                        spamThresholdText = null;
                        contextualWeightText = null;
                    }
                    else
                    {
                        string clippingScoreText = helperFuntion(zoneData, "ClippingScore");
                        if (!clippingScoreText.Equals(String.Empty))
                        {
                            string key = typeOfAnalysis.ToString() + "#" + zoneName + "#" + "E" + "#" + typeOfWordScore.ToString();
                            _zoneAttributePropertiesLookup.Add(key, double.Parse(clippingScoreText));
                        }
                        clippingScoreText = null;
                    }

                }
                catch (Exception ex)
                {
                    Trace.TraceError(DateTime.Now.ToString() + " : " + "Failed to load tag definition. Reason: " + ex.Message);
                    Trace.TraceError(DateTime.Now.ToString() + " : " + "--------------");
                    Trace.TraceError(DateTime.Now.ToString() + " : " + "Tag Definition:" + zoneData.InnerXml);
                }
            }
        }

        public static void StartAllBackgroundWorkerThreads()
        {
            // Start all background worker threads
            StartAlexaDataLoader();
            StartWorkFlowManagers();
            StartTicketsEmailProcessor();
            StartPaypalReconciler();
        }

        public static void StopAllBackgroundWorkerThreads()
        {
            // Signal stop to all background worker threads

            StopAlexaDataLoader();
            StopWorkFlowManagers();
            StopTicketsEmailProcessor();
            StopPaypalReconciler();

            // Wait for all background threads to complete

            WaitStopTicketsEmailProcessor();
            WaitStopPaypalReconciler();
            WaitStopAlexaDataLoader();
            WaitStopWorkFlowManagers();
        }


        /// <summary>
        /// Method to unlock the jobs left locked (before workflow managers could stop cleanly) during last App Run
        /// </summary>
        public static void UnlockAllWorkFlowJobs()
        {
            Trace.TraceInformation(DateTime.Now + " : Workflow: UnlockAllWorkFlowJobs");
            _workPool.UnlockAllJobs(ApplicationConfig.AppInstanceId);
        }

        /// <summary>
        /// Method to start work flow managers
        /// </summary>
        public static void StartWorkFlowManagers()
        {
            // Unlock the workflow jobs left locked during last App run
            // Sudden shutdown of app is not expected. So only the failed job (StackOverflow or OutofMemory) will be left locked.
            // Because if a bad job keeps on crashing the app. , then it will go in a cycle by getting unlocked everytime at start 
            // and no other jobs will get a chance to execute.

            // Those locked(failed) jobs need engg. investigation, dont unlock for now.
            // ApplicationConfig.UnlockAllWorkFlowJobs();
            
            if (bRunWorkflowManagers)
            {
                Trace.TraceInformation(DateTime.Now + " : Workflow: Start Managers");
                _wfmThread.Start();
            }
        }

        /// <summary>
        /// Method to stop work flow managers
        /// </summary>
        public static void StopWorkFlowManagers()
        {
            if (bRunWorkflowManagers)
            {
                Trace.TraceInformation(DateTime.Now + " : Workflow : Stop Managers");
                //Stop Manager
                _workFlowManager.StopManager();
            }
        }

        public static void WaitStopWorkFlowManagers()
        {
            if (bRunWorkflowManagers || _wfmThread.IsAlive)
            {
                Trace.TraceInformation(DateTime.Now + " : Workflow : Wait for Managers");
                while (_wfmThread.IsAlive)
                {
                    Thread.Sleep(1000 * 60);
                }
                Trace.TraceInformation(DateTime.Now + " : Workflow : Finished Managers");
            }
        }

        /// <summary>
        /// Method to start the tickets email processor
        /// </summary>
        public static void StartTicketsEmailProcessor()
        {
            if (bRunTicketsEmailProcessor)
            {
                _bwTicketsProcessor.DoWork += ProcessTicketsEmail;
                TimeSpan dueTime = new TimeSpan(0, 1, 0);
                TimeSpan period = new TimeSpan(0, _ticketsProcessorPeriodInMinutes, 0);
                _ticketsEmailProcessor = new Timer(_bwTicketsProcessor.RunWorkerAsync, null, dueTime, period);
            }
        }

        /// <summary>
        /// Delegate to run the tickets email processor by the timer
        /// </summary>
        static void ProcessTicketsEmail(object sender, DoWorkEventArgs e)
        {
            SupportMailHandler.ReadAndProcessTicketsMailBox();
        }

        /// <summary>
        /// Method to stop the tickets email processor
        /// </summary>
        public static void StopTicketsEmailProcessor()
        {
            if (bRunTicketsEmailProcessor)
            {
                // Set timer to infinite values, so that it doesnt trigger again
                _ticketsEmailProcessor.Change(int.MaxValue, int.MaxValue);
            }
        }

        public static void WaitStopTicketsEmailProcessor()
        {
            Trace.TraceInformation(DateTime.Now + " : TicketsProcessor: Waiting to finish");

            while (_bwTicketsProcessor.IsBusy)
                Thread.Sleep(1000);

            if (_ticketsEmailProcessor != null)
            {
                //_ticketsEmailProcessor.InitializeLifetimeService()
                _ticketsEmailProcessor.Dispose();
            }

            _bwTicketsProcessor.Dispose();

            Trace.TraceInformation(DateTime.Now + " : TicketsProcessor: Finished");
        }

        /// <summary>
        /// Method to start paypal reconciler thread
        /// </summary>
        public static void StartPaypalReconciler()
        {
            if (bRunPaypalReconciler)
            {
                Trace.TraceInformation(DateTime.Now + " : Paypal: Start Reconciler");
                _paypalThread.Start();
            }
        }

        /// <summary>
        /// Method to stop paypal reconciler thread
        /// </summary>
        public static void StopPaypalReconciler()
        {
            if (bRunPaypalReconciler || _paypalThread.IsAlive)
            {
                Trace.TraceInformation(DateTime.Now + " : Paypal: StopReconciler");
                //Stop Reconciler
                _paypalReconciler.Stop();

                // Interrupt the thread out of sleep if it is
                _paypalThread.Interrupt();
            }
        }

        public static void WaitStopPaypalReconciler()
        {
            if (bRunPaypalReconciler || _paypalThread.IsAlive)
            {
                Trace.TraceInformation(DateTime.Now + " : Paypal: Wait Reconciler");

                while (_paypalThread.IsAlive)
                {
                    Thread.Sleep(1000 * 10);
                }
                Trace.TraceInformation(DateTime.Now + " : Paypal: Finished Reconciler");
            }
        }

        /// <summary>
        /// Method that loads all the tag configuration definitions
        /// </summary>  
        public static void LoadTagDefinitions()
        {
            XmlDocument xDoc = new XmlDocument();

            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/TagDefinitions.xml");

            _tagDefinitions.Clear();

            foreach (XmlElement tagDefinition in xDoc.GetElementsByTagName("Tag"))
            {
                try
                {
                    TagConfiguration tc = new TagConfiguration();
                    tc.TagName = tagDefinition.GetElementsByTagName("TagName")[0].InnerText;
                    tc.ProcessTag = bool.Parse(tagDefinition.GetElementsByTagName("ProcessTag")[0].InnerText);
                    tc.EvalCssFormat = bool.Parse(tagDefinition.GetElementsByTagName("EvalCssFormat")[0].InnerText);
                    tc.AddNameMarker = bool.Parse(tagDefinition.GetElementsByTagName("AddNameMarker")[0].InnerText);
                    tc.TagWeight = double.Parse(tagDefinition.GetElementsByTagName("TagWeight")[0].InnerText);
                    tc.MaxCount = double.Parse(tagDefinition.GetElementsByTagName("MaxCount")[0].InnerText);
                    tc.TransformTag = bool.Parse(tagDefinition.GetElementsByTagName("TransformTag")[0].InnerText);
                    tc.Transformation = tagDefinition.GetElementsByTagName("Transformation")[0].InnerText;
                    tc.WhichTagValue = tagDefinition.GetElementsByTagName("WhichTagValue")[0].InnerText;
                    tc.WhichAttributes = tagDefinition.GetElementsByTagName("WhichAttributes")[0].InnerText;
                    tc.AddToZoneTermMatrix = bool.Parse(tagDefinition.GetElementsByTagName("AddToZoneTermMatrix")[0].InnerText);
                    tc.AddToWhichZones = tagDefinition.GetElementsByTagName("AddToWhichZones")[0].InnerText;
                    tc.NumberingScheme = tagDefinition.GetElementsByTagName("NumberingScheme")[0].InnerText;

                    _tagDefinitions.Add(tc.TagName, tc);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(DateTime.Now.ToString() + " : " + "Failed to load tag definition. Reason: " + ex.Message);
                    Trace.TraceError(DateTime.Now.ToString() + " : " + "--------------");
                    Trace.TraceError(DateTime.Now.ToString() + " : " + "Tag Definition:" + tagDefinition.InnerXml);
                }
            }
        }

        /// <summary>
        /// Method that loads stop words in a given language setting
        /// Currently this method only works for English
        /// </summary>  
        public static void LoadStopWords()
        {
            XmlDocument xDoc = new XmlDocument();

            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/StopWords_en-US.xml");

            _stopWords.Clear();

            foreach (XmlNode word in xDoc.GetElementsByTagName("Word"))
            {
                _stopWords.Add(word.InnerText);
            }
        }

        /// <summary>
        /// Method that loads domain suffix tokens
        /// </summary>  
        public static void LoadDomainSuffixTokens()
        {
            XmlDocument xDoc = new XmlDocument();

            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/DomainSuffixTokens.xml");

            _domainSuffixTokens.Clear();

            foreach (XmlNode word in xDoc.GetElementsByTagName("Token"))
            {
                _domainSuffixTokens.Add(word.InnerText);
            }
        }

        /// <summary>
        /// Method that loads inheritable css properties
        /// </summary>  
        public static void LoadInheritableCssProperties()
        {
            XmlDocument xDoc = new XmlDocument();

            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/InheritableCssProperties.xml");

            _inheritableCssProperties.Clear();

            foreach (XmlNode word in xDoc.GetElementsByTagName("Property"))
            {
                _inheritableCssProperties.Add(word.InnerText);
            }
        }


        /// <summary>
        /// Method that loads stop words in a given language setting
        /// Currently this method only works for English
        /// </summary>  
        public static void LoadHostNormalizationExclusions()
        {
            XmlDocument xDoc = new XmlDocument();

            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/HostsIgnoredInNormalization.xml");

            _excludeHostsFromNormalization.Clear();

            foreach (XmlNode host in xDoc.GetElementsByTagName("Host"))
            {
                _excludeHostsFromNormalization.Add(host.InnerText);
            }
        }

        /// <summary>
        /// Method that converts xml inputs of a rule to a List 
        /// </summary>  
        /// <param name="input">XmlNode that contains input parameters of a given rule</param>
        /// <returns>A lookup list of param names and values</returns>
        private static List<StockTrader.Platform.Database.Platform.Utilities.Common.KeyValuePair<string, string>> SetupOutputParamsHash(XmlNode input)
        {
            List<StockTrader.Platform.Database.Platform.Utilities.Common.KeyValuePair<string, string>> parameters = new List<StockTrader.Platform.Database.Platform.Utilities.Common.KeyValuePair<string, string>>();

            if (!input.HasChildNodes)
                return parameters;

            foreach (XmlNode param in input.ChildNodes)
            {
                StockTrader.Platform.Database.Platform.Utilities.Common.KeyValuePair<string, string> kvp = new StockTrader.Platform.Database.Platform.Utilities.Common.KeyValuePair<string, string>();
                kvp.Key = param.Name;
                kvp.Value = param.InnerText;
                parameters.Add(kvp);
            }

            //parameters.Sort(SEOTools.Common.KeyValuePairComparer.SortKeys());

            return parameters;
        }

        /// <summary>
        /// Method that loads configured rules
        /// </summary>  
        public static void LoadRuleBook()
        {
            XmlDocument xDoc = new XmlDocument();

            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/RuleBook.xml");

            _ruleBook.Clear();

            foreach (XmlElement rule in xDoc.GetElementsByTagName("Rule"))
            {
                try
                {
                    RuleDefinition rd = new RuleDefinition();
                    rd.Id = int.Parse(rule.GetElementsByTagName("Id")[0].InnerText);
                    rd.Name = rule.GetElementsByTagName("Name")[0].InnerText;
                    rd.SequenceNo = int.Parse(rule.GetElementsByTagName("SequenceNo")[0].InnerText);
                    rd.Categorization = short.Parse(rule.GetElementsByTagName("Categorization")[0].InnerText);
                    rd.Assembly = rule.GetElementsByTagName("Assembly")[0].InnerText;
                    rd.SearchEngine = short.Parse(rule.GetElementsByTagName("SearchEngine")[0].InnerText);
                    rd.Input = (XmlNode)rule.GetElementsByTagName("Input")[0];
                    rd.AlertMessage = (string)rule.GetElementsByTagName("AlertMessage")[0].InnerText;
                    rd.Output = SetupOutputParamsHash(rd.Input);
                    rd.ContextDocType = int.Parse(rule.GetElementsByTagName("ContextDocType")[0].InnerText);
                    rd.ActiveStatus = short.Parse(rule.GetElementsByTagName("ActiveStatus")[0].InnerText);

                    if (rd.ActiveStatus == 1)
                        _ruleBook.Add(rd);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(DateTime.Now.ToString() + " : " + "Failed to load the rule into rulebook. Reason: " + ex.Message);

                }
            }
        }

        /// <summary>
        /// Method that loads filetered urls table
        /// </summary>  
        public static void LoadFilteredUrls()
        {
            XmlDocument xDoc = new XmlDocument();

            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/FilteredUrls.xml");

            _filteredUrls.Clear();

            foreach (XmlElement url in xDoc.GetElementsByTagName("url"))
            {
                try
                {
                    var name = url.GetElementsByTagName("name")[0].InnerText;
                    var type = url.GetElementsByTagName("type")[0].InnerText;

                    if (!UtilConstants.HTTP_SCHEME_REGEX.IsMatch(name))
                    {
                        name = UtilConstants.HTTP_SCHEME + name;
                    }

                    var normalizedUrl = WebUtilityLibrary.GetNormalizedUrl(name.ToLower(), true);

                    _filteredUrls.Add(normalizedUrl, type);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(string.Format(DateTime.Now + " : " + "Failed to load filtered urls. Reason: {0} \n StackTrace: {1}" + ex.Message, ex.StackTrace));
                }
            }
        }


        /// <summary>
        /// Method that loads email parameters table
        /// </summary>  
        public static void LoadEmailConfiguration()
        {
            _emailConfiguration.Clear();

            LoadEmailTemplates();

            LoadEmailParameters();
        }

        private static TemplateType MapEmailParameterToEmailType(string parameterName)
        {
            TemplateType emailType = TemplateType.Unknown;

            switch (parameterName)
            {
                case "UserAutoSignupNotification":
                    emailType = TemplateType.UserAutoSignupNotification;
                    break;

                case "UserSignupWelcome":
                    emailType = TemplateType.UserSignupWelcome;
                    break;

                case "UserSignupVerification":
                    emailType = TemplateType.UserSignupVerification;
                    break;

                case "PasswordRecovery":
                    emailType = TemplateType.PasswordRecovery;
                    break;

                case "PaymentStatus":
                    emailType = TemplateType.PaymentStatus;
                    break;

                case "ReportExecutionPaymentSuccess":
                    emailType = TemplateType.ReportExecutionPaymentSuccess;
                    break;
                    
                case "JobScheduled":
                    emailType = TemplateType.JobScheduled;
                    break;

                case "JobCompleted":
                    emailType = TemplateType.JobCompleted;
                    break;

                case "JobFailedInternal":
                    emailType = TemplateType.JobFailedInternal;
                    break;

                case "JobFailedUser":
                    emailType = TemplateType.JobFailedUser;
                    break;

                case "TrialUserJobScheduled":
                    emailType = TemplateType.TrialUserJobScheduled;
                    break;

                case "TrialUserJobCompleted":
                    emailType = TemplateType.TrialUserJobCompleted;
                    break;

                case "UserTicketUpdated":
                    emailType = TemplateType.UserTicketUpdated;
                    break;

                case "ErrorInternal":
                    emailType = TemplateType.ErrorInternal;
                    break;

                case "TrialUserJobCompletedNotification":
                    emailType = TemplateType.TrialUserJobCompletedNotification;
                    break;

                case "JobCompletedNotification":
                    emailType = TemplateType.JobCompletedNotification;
                    break;

                case "DownloadInvoice":
                    emailType = TemplateType.DownloadInvoice;
                    break;
            }

            return emailType;

        }


        /// <summary>
        /// Method that loads email templates table
        /// </summary>  
        private static void LoadEmailTemplates()
        {
            string templateDir = ApplicationConfig.PhysicalFilePath + "/App_Data/ConfigData/EmailConfiguration/BodyTemplates/";
            var templateFiles = Directory.EnumerateFiles(templateDir);

            //_emailTemplates.Clear();

            foreach (var templateFile in templateFiles)
            {
                try
                {
                    FileInfo fi = new FileInfo(templateFile);

                    Emails.EmailConfig email = new Emails.EmailConfig();
                    var templateName = fi.Name.Remove(fi.Name.IndexOf(fi.Extension));

                    var emailType = MapEmailParameterToEmailType(templateName);

                    var emailTypeString = emailType.ToString();

                    var body = File.ReadAllText(templateFile);

                    if (!_emailConfiguration.ContainsKey(emailTypeString))
                    {
                        _emailConfiguration[emailTypeString] = new Emails.EmailConfig();
                    }

                    var config = (Emails.EmailConfig)_emailConfiguration[emailTypeString];

                    config.Body = body;

                    config.EmailType = emailType;

                }
                catch (Exception ex)
                {
                    Trace.TraceError(string.Format(DateTime.Now + " : " + "Failed to load email template {2}. Reason: {0} \n StackTrace: {1}" + 
                        ex.Message, ex.StackTrace, templateFile));
                }
            }

        }

        /// <summary>
        /// Method that loads email parameters table
        /// </summary>  
        private static void LoadEmailParameters()
        {
            XmlDocument xDoc = new XmlDocument();

            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/EmailConfiguration/EmailConfiguration.xml");

            foreach (XmlElement email in xDoc.GetElementsByTagName("email"))
            {
                try
                {
                    var emailName = email.GetElementsByTagName("name")[0].InnerText;
                    var emailType = MapEmailParameterToEmailType(emailName);
                    var emailTypeString = emailType.ToString();
                    var subject = email.GetElementsByTagName("subject")[0].InnerText;

                    if (!_emailConfiguration.ContainsKey(emailTypeString))
                    {
                        _emailConfiguration[emailTypeString] = new Emails.EmailConfig();
                    }

                    var config = (Emails.EmailConfig)_emailConfiguration[emailTypeString];

                    config.Subject = subject;

                    config.EmailType = emailType;
                   
                }
                catch (Exception ex)
                {
                    Trace.TraceError(string.Format(DateTime.Now + " : " + "Failed to load email parameters. Reason: {0} \n StackTrace: {1}" + ex.Message, ex.StackTrace));
                }
            }

        }


        /// <summary>
        /// Method that loads application parameters
        /// </summary>  
        public static void LoadApplicationParameters()
        {
            XmlDocument xDoc = new XmlDocument();

            xDoc.Load(_physicalFilePath + "/App_Data/ConfigData/ApplicationParameters.xml");

            _applParams.Clear();

            foreach (XmlElement applParam in xDoc.GetElementsByTagName("Param"))
            {
                try
                {
                    ApplicationParameter ap = new ApplicationParameter();
                    ap.ParamProduct = applParam.GetElementsByTagName("Product")[0].InnerText;
                    ap.ParamName = applParam.GetElementsByTagName("Name")[0].InnerText;
                    ap.ParamValue = applParam.GetElementsByTagName("Value")[0].InnerText;
                    ap.ActiveStatus = short.Parse(applParam.GetElementsByTagName("ActiveStatus")[0].InnerText);

                    if (ap.ActiveStatus == 1)
                        _applParams.Add(ap.ParamName, ap);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(DateTime.Now.ToString() + " : " + "Failed to load application parameter. Reason: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Method that loads default CSS Styles
        /// </summary>  
        public static void LoadDefaultCssStyles()
        {
            string defaultCssFile = _physicalFilePath + "/Css/Default.css";

            try
            {
                _defaultCssStyles = FileUtilityLibrary.ReadTextFromFile(defaultCssFile);
            }
            catch (Exception ex)
            {
                Trace.TraceError(DateTime.Now.ToString() + " : " + "Failed to load default css styles. Reason: " + ex.Message);
            }
        }

        /// <summary>
        /// Method to update database with cached alexa datapoints
        /// </summary>
        private static void InsertIntoAlexaTables()
        {

            //Insert alexa datapoints parallelly
           foreach (string url in AlexaDetails.Keys)
            {
                // Create database related library file instance
                DBLibrary dbLib = new DBLibrary();

                //Create Database Utillties library instance
                DBUtilities dbUtilities = new DBUtilities();

                //Create param list
                ArrayList paramList = new ArrayList();

                try
                {
                    //insert into alexaUrlInfo
                    paramList.Clear();

                    dbLib.ExecuteProcedure("sp_insert_pdr_alexaUrlInfo", AlexaDetails[url].DbParamList(0));

                    //insert into Categories table
                    foreach (OpenDirectoryCategory odc in AlexaDetails[url].Categories)
                    {
                        paramList = odc.DbParamList(0);
                        paramList.Add(dbUtilities.CreateSqlParamater("@inputUrl", SqlDbType.VarChar, ParameterDirection.Input, AlexaDetails[url].OriginalUrl));
                        paramList.Add(dbUtilities.CreateSqlParamater("@alexaUrl", SqlDbType.VarChar, ParameterDirection.Input, AlexaDetails[url].Url));

                        dbLib.ExecuteProcedure("sp_insert_pdr_alexaRelatedCategories", paramList);
                    }

                    //insert into related links table
                    foreach (RelatedLink rl in AlexaDetails[url].RelatedLinks)
                    {
                        paramList = rl.DbParamList(0);
                        paramList.Add(dbUtilities.CreateSqlParamater("@inputUrl", SqlDbType.VarChar, ParameterDirection.Input, AlexaDetails[url].OriginalUrl));
                        paramList.Add(dbUtilities.CreateSqlParamater("@alexaUrl", SqlDbType.VarChar, ParameterDirection.Input, AlexaDetails[url].Url));

                        dbLib.ExecuteProcedure("sp_insert_pdr_alexaRelatedLinks", paramList);
                    }

                    //insert into keywords table
                    foreach (string keyword in AlexaDetails[url].Keywords)
                    {
                        paramList.Clear();
                        paramList.Add(dbUtilities.CreateSqlParamater("@inputUrl", SqlDbType.VarChar, ParameterDirection.Input, AlexaDetails[url].OriginalUrl));
                        paramList.Add(dbUtilities.CreateSqlParamater("@alexaUrl", SqlDbType.VarChar, ParameterDirection.Input, AlexaDetails[url].Url));
                        paramList.Add(dbUtilities.CreateSqlParamater("@keyword", SqlDbType.NVarChar, ParameterDirection.Input, keyword));
                        paramList.Add(dbUtilities.CreateSqlParamater("@insertStatus", SqlDbType.Bit, ParameterDirection.Output));

                        dbLib.ExecuteProcedure("sp_insert_pdr_alexaRelatedKeywords", paramList);
                    }

                    //insert into otherdomains table
                    foreach (OwnedDomain od in AlexaDetails[url].OtherOwnedDomains)
                    {
                        paramList = od.DbParamList(0);
                        paramList.Add(dbUtilities.CreateSqlParamater("@inputUrl", SqlDbType.VarChar, ParameterDirection.Input, AlexaDetails[url].OriginalUrl));
                        paramList.Add(dbUtilities.CreateSqlParamater("@alexaUrl", SqlDbType.VarChar, ParameterDirection.Input, AlexaDetails[url].Url));

                        dbLib.ExecuteProcedure("sp_insert_pdr_alexaOwnedDomains", paramList);
                    }

                    //insert into subdomains table
                    foreach (ContributingSubDomain csd in AlexaDetails[url].ContributingSubDomains)
                    {
                        paramList = csd.DbParamList(0);
                        paramList.Add(dbUtilities.CreateSqlParamater("@inputUrl", SqlDbType.VarChar, ParameterDirection.Input, AlexaDetails[url].OriginalUrl));
                        paramList.Add(dbUtilities.CreateSqlParamater("@alexaUrl", SqlDbType.VarChar, ParameterDirection.Input, AlexaDetails[url].Url));

                        dbLib.ExecuteProcedure("sp_insert_pdr_alexaSubdomainContributions", paramList);
                    }

                    //insert into stats table
                    foreach (AlexaUsageStatistic us in AlexaDetails[url].UsageStatistics)
                    {
                        paramList = us.DbParamList(0);
                        paramList.Add(dbUtilities.CreateSqlParamater("@alexaUrl", SqlDbType.VarChar, ParameterDirection.Input, AlexaDetails[url].Url));
                        paramList.Add(dbUtilities.CreateSqlParamater("@inputUrl", SqlDbType.VarChar, ParameterDirection.Input, AlexaDetails[url].OriginalUrl));

                        dbLib.ExecuteProcedure("sp_insert_pdr_alexaUsageStatistics", paramList);
                    }

                    //insert into logs table
                    paramList.Clear();

                    paramList.Add(dbUtilities.CreateSqlParamater("@inputUrl", SqlDbType.VarChar, ParameterDirection.Input, url));
                    paramList.Add(dbUtilities.CreateSqlParamater("@alexaUrl", SqlDbType.VarChar, ParameterDirection.Input, AlexaDetails[url].Url));
                    paramList.Add(dbUtilities.CreateSqlParamater("@flag", SqlDbType.Int, ParameterDirection.Input, AlexaDetails[url].PacketStatus));
                    paramList.Add(dbUtilities.CreateSqlParamater("@noOfRetriesToAlexa", SqlDbType.Int, ParameterDirection.Input, 1));
                    paramList.Add(dbUtilities.CreateSqlParamater("@roundTripTime", SqlDbType.Decimal, ParameterDirection.Input, 0));
                    paramList.Add(dbUtilities.CreateSqlParamater("@responseParseTime", SqlDbType.Decimal, ParameterDirection.Input, 0));
                    paramList.Add(dbUtilities.CreateSqlParamater("@queryPreparationTime", SqlDbType.Decimal, ParameterDirection.Input, 0));
                    paramList.Add(dbUtilities.CreateSqlParamater("@databaseTxnTime", SqlDbType.Decimal, ParameterDirection.Input, 0));
                    paramList.Add(dbUtilities.CreateSqlParamater("@modifiedTime", SqlDbType.DateTime, ParameterDirection.Input, DateTime.Now));
                    paramList.Add(dbUtilities.CreateSqlParamater("@insertStatus", SqlDbType.Bit, ParameterDirection.Output));

                    dbLib.ExecuteProcedure("sp_insert_pdr_alexaUrlLogs", paramList);
                }
                catch (Exception ex)
                {
                    //Something went wrong - we will clean up the data
                    paramList.Clear();
                    //It's better to keep it in one transaction
                    Trace.TraceError(DateTime.Now.ToString() + " : " + "Alexa Update: Failed to process Database insertion procedures. Reason: " + ex.Message);
                }
                finally
                {
                    paramList = null;
                }
            }
        }

        /// <summary>
        /// Method to load global cache into database
        /// </summary>
        private static void InsertAlexaData()
        {
            try
            {
                _alexaDetailsLock.EnterWriteLock();

                InsertIntoAlexaTables();

                //clear
                AlexaDetails.Clear();
            }
            catch (Exception ex)
            {
                //something is wrong
                Trace.TraceError(DateTime.Now.ToString() + " : " + "Alexa Update: Failed to update Database. Reason: " + ex.Message);
            }
            finally
            {
                //release the lock
                _alexaDetailsLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Method that checks whether the global cache is full or not and when it is full, updates database
        /// </summary>
        private static void StartAlexaDataLoader()
        {
            _isAlexaActive = true;
            Trace.TraceInformation(DateTime.Now + " : Alexa: Start Dataloader");
           
            _alexaThread.Start();
        }

        private static void DoAlexaDataLoad()
        {
            while (_isAlexaActive)
            {
                //check if count of cached urls is greater than 1000, and if so, add it to database and clear cache
                if (AlexaDetails.Count > 100)
                {
                    InsertAlexaData();
                }

                try
                {
                    if (_isAlexaActive)
                        Thread.Sleep(600000);
                }
                catch (ThreadInterruptedException)
                {
                    Trace.TraceInformation(DateTime.Now + " : Alexa:  Interrupted Dataloader");
                }
            }
        }
        
        /// <summary>
        /// Method to stop alexa data loading
        /// </summary>
        public static void StopAlexaDataLoader()
        {
            Trace.TraceInformation(DateTime.Now + " : Alexa: Stop Dataloader");

            _isAlexaActive = false;

            //Stop the loading thread
            _alexaThread.Interrupt();
        }

        public static void WaitStopAlexaDataLoader()
        {
            Trace.TraceInformation(DateTime.Now + " : Alexa: Wait Dataloader");
            while (_alexaThread.IsAlive)
            {
                Thread.Sleep(1000 * 10);
            }

            //Insert remaining alexa data cache entries to database
            InsertAlexaData();

            Trace.TraceInformation(DateTime.Now + " : Alexa: Finished Dataloader");
        }

        /// <summary>
        /// Method to insert alexa datapoint into global cache
        /// </summary>
        /// <param name="alexaUrl">Url to be inserted</param>
        /// <param name="ar">Alexa Response object</param>
        public static void UpdateAlexaGlobalCache(string alexaUrl, AlexaResponse ar)
        {
            _alexaDetailsLock.EnterWriteLock();
            try
            {
                if (!_alexaDetails.ContainsKey(alexaUrl))
                {
                    _alexaDetails.Add(alexaUrl, ar);
                }
                else
                {
                    _alexaDetails[alexaUrl] = ar;
                }
            }
            catch (Exception ex)
            {
                //something is wrong
                Trace.TraceError(DateTime.Now.ToString() + " : " + "Alexa Update: Failed to update global cache. Reason: " + ex.Message);
            }
            finally
            {
                //release the lock
                _alexaDetailsLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Method to update a public list of bad hosts
        /// </summary>
        /// <param name="badHost">Bad host</param>
        public static void UpdateBadHost(string badHost)
        {
            _badHostsLock.EnterWriteLock();

            try
            {
                _badHosts.Add(badHost);
            }
            finally
            {
                _badHostsLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Method to upate last url request times
        /// </summary>
        /// <param name="urlDomain">Domain that we are currently requesting</param>
        /// <param name="requestTime">Time the request is being sent out</param>
        public static void UpdateUrlRequestTimes(string urlDomain, DateTime requestTime)
        {
            _urlRequestTimesLock.EnterWriteLock();

            try
            {
                _urlRequestTimes.Add(urlDomain, requestTime);
            }
            finally
            {
                _urlRequestTimesLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Method to Inverses of Fibonacci values for quick reference
        /// </summary>
        private static void LoadFibonacciValues()
        {
            for (int i = 0; i < 30; i++)
            {
                _fibCache[i] = (double)1 / (double)Combinatorics.Fibonacci(i + 1);
            }
        }

        /// <summary>
        /// Method to load query weight lookup
        /// </summary>
        private static void LoadQueryWeightLookup()
        {
            double queryWeightFactor = double.Parse(((ApplicationParameter)ApplicationConfig.ApplParams["QueryWeightFactor"]).ParamValue);

            for (int i = 0; i < 5; i++)
            {
                _queryWeightLookup[i] = Math.Pow(2, queryWeightFactor * i);
            }
        }

        /// <summary>
        /// Method to load application cache
        /// </summary>  
        public static void LoadApplicationCache()
        {
            LoadUrlCatagory();
            LoadApplicationParameters();
            LoadStopWords();
            LoadDomainSuffixTokens();
            LoadInheritableCssProperties();
            LoadHostNormalizationExclusions();
            LoadTagDefinitions();
            LoadRuleBook();
            LoadDefaultCssStyles();
            LoadFibonacciValues();
            LoadQueryWeightLookup();
            LoadWeights();
            LoadFilteredUrls();
            LoadEmailConfiguration();
        }
    }

}
