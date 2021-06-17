using System;
using System.Collections.Generic;

// This class will read a tab-delimited text file with peptides and scores
// and create a new PepXML or mzIdentML file with the peptides
//
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
//
// Started April 13, 2012

using System.IO;
using System.Linq;
using PRISM;

namespace PeptideListToXML
{
    public class clsPeptideListToXML : PRISM.FileProcessor.ProcessFilesBase
    {

        // Ignore Spelling: mzIdentML, Wiff

        public clsPeptideListToXML()
        {
            mFileDate = "June 17, 2021";
            InitializeLocalVariables();
        }

        #region Constants and Enums

        public const string XML_SECTION_OPTIONS = "PeptideListToXMLOptions";
        public const int DEFAULT_HITS_PER_SPECTRUM = 3;
        public const int DEFAULT_MAX_PROTEINS_PER_PSM = 100;
        protected const int PREVIEW_PAD_WIDTH = 22;

        // Future enum; mzIdentML is not yet supported
        // Public Enum PeptideListOutputFormat
        // PepXML = 0
        // mzIdentML = 1
        // End Enum

        /// <summary>
    /// Error codes specialized for this class
    /// </summary>
    /// <remarks></remarks>
        public enum PeptideListToXMLErrorCodes
        {
            NoError = 0,
            ErrorReadingInputFile = 1,
            ErrorWritingOutputFile = 2,
            ModSummaryFileNotFound = 3,
            SeqInfoFileNotFound = 4,
            MSGFStatsFileNotFound = 5,
            ScanStatsFileNotFound = 6,
            UnspecifiedError = -1
        }

        #endregion

        #region Structures

        #endregion

        #region Class wide Variables

        // Future enum; mzIdentML is not yet supported
        // Protected mOutputFormat As clsPeptideListToXML.PeptideListOutputFormat

        protected PHRPReader.ReaderFactory mPHRPReader;
        protected clsPepXMLWriter mXMLWriter;

        // Note that DatasetName is auto-determined via ConvertPHRPDataToXML()
        protected string mDatasetName;
        protected PHRPReader.PeptideHitResultTypes mPeptideHitResultType;
        protected SortedList<int, List<PHRPReader.Data.ProteinInfo>> mSeqToProteinMapCached;

        // Note that FastaFilePath will be ignored if the Search Engine Param File exists and it contains a fasta file name
        protected string mFastaFilePath;
        protected string mSearchEngineParamFileName;
        protected int mHitsPerSpectrum;                // Number of hits per spectrum to store; 0 means to store all hits
        protected bool mPreviewMode;
        protected bool mSkipXPeptides;
        protected bool mTopHitOnly;
        protected int mMaxProteinsPerPSM;
        protected string mPeptideFilterFilePath;
        protected List<int> mChargeFilterList;
        protected bool mLoadModsAndSeqInfo;
        protected bool mLoadMSGFResults;
        protected bool mLoadScanStats;

        // This dictionary tracks the PSMs (hits) for each spectrum
        // The key is the Spectrum Key string (dataset, start scan, end scan, charge)
        protected Dictionary<string, List<PHRPReader.Data.PSM>> mPSMsBySpectrumKey;

        // This dictionary tracks the spectrum info
        // The key is the Spectrum Key string (dataset, start scan, end scan, charge)
        protected Dictionary<string, clsPepXMLWriter.udtSpectrumInfoType> mSpectrumInfo;
        private PeptideListToXMLErrorCodes mLocalErrorCode;
        #endregion

        #region Processing Options Interface Functions

        public List<int> ChargeFilterList
        {
            get
            {
                return mChargeFilterList;
            }

            set
            {
                if (value is null)
                {
                    mChargeFilterList = new List<int>();
                }
                else
                {
                    mChargeFilterList = value;
                }
            }
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
    /// Dataset name; auto-determined by the PHRP Reader class
    /// </summary>
    /// <value></value>
    /// <returns></returns>
    /// <remarks></remarks>
        public string DatasetName
        {
            get
            {
                return mDatasetName;
            }
        }

        /// <summary>
    /// Fasta file path to store in the pepXML file
    /// Ignored if the Search Engine Param File exists and it contains a fasta file name (typically the case for SEQUEST and X!Tandem)
    /// </summary>
    /// <value></value>
    /// <returns></returns>
    /// <remarks></remarks>
        public string FastaFilePath
        {
            get
            {
                return mFastaFilePath;
            }

            set
            {
                mFastaFilePath = value;
            }
        }

        /// <summary>
    /// Number of peptides per spectrum to store in the PepXML file; 0 means store all hits
    /// </summary>
    /// <value></value>
    /// <returns></returns>
    /// <remarks></remarks>
        public int HitsPerSpectrum
        {
            get
            {
                return mHitsPerSpectrum;
            }

            set
            {
                mHitsPerSpectrum = value;
            }
        }

        public bool LoadModsAndSeqInfo
        {
            get
            {
                return mLoadModsAndSeqInfo;
            }

            set
            {
                mLoadModsAndSeqInfo = value;
            }
        }

        public bool LoadMSGFResults
        {
            get
            {
                return mLoadMSGFResults;
            }

            set
            {
                mLoadMSGFResults = value;
            }
        }

        public bool LoadScanStats
        {
            get
            {
                return mLoadScanStats;
            }

            set
            {
                mLoadScanStats = value;
            }
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
    /// Local error code
    /// </summary>
    /// <value></value>
    /// <returns></returns>
    /// <remarks></remarks>
        public PeptideListToXMLErrorCodes LocalErrorCode
        {
            get
            {
                return mLocalErrorCode;
            }
        }

        public int MaxProteinsPerPSM
        {
            get
            {
                return mMaxProteinsPerPSM;
            }

            set
            {
                mMaxProteinsPerPSM = value;
            }
        }

        /// <summary>
    /// If true, then displays the names of the files that are required to create the PepXML file for the specified dataset
    /// </summary>
    /// <value></value>
    /// <returns></returns>
    /// <remarks></remarks>
        public bool PreviewMode
        {
            get
            {
                return mPreviewMode;
            }

            set
            {
                mPreviewMode = value;
            }
        }

        /// <summary>
    /// Name of the parameter file used by the search engine that produced the results file that we are parsing
    /// </summary>
    /// <value></value>
    /// <returns></returns>
    /// <remarks></remarks>
        public string SearchEngineParamFileName
        {
            get
            {
                return mSearchEngineParamFileName;
            }

            set
            {
                mSearchEngineParamFileName = value;
            }
        }

        /// <summary>
    /// If True, then skip peptides with X residues
    /// </summary>
    /// <value></value>
    /// <returns></returns>
    /// <remarks></remarks>
        public bool SkipXPeptides
        {
            get
            {
                return mSkipXPeptides;
            }

            set
            {
                mSkipXPeptides = value;
            }
        }

        /// <summary>
    /// If True, then only keeps the top-scoring peptide for each scan number
    /// </summary>
    /// <value></value>
    /// <returns></returns>
    /// <remarks>If the scan has multiple charges, the output file will still only have one peptide listed for that scan number</remarks>
        public bool TopHitOnly
        {
            get
            {
                return mTopHitOnly;
            }

            set
            {
                mTopHitOnly = value;
            }
        }

        public string PeptideFilterFilePath
        {
            get
            {
                return mPeptideFilterFilePath;
            }

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    mPeptideFilterFilePath = string.Empty;
                }
                else
                {
                    mPeptideFilterFilePath = value;
                }
            }
        }

        // Future enum; mzIdentML is not yet supported
        // Public Property OutputFormat() As PeptideListOutputFormat
        // Get
        // Return mOutputFormat
        // End Get
        // Set(value As PeptideListOutputFormat)
        // mOutputFormat = value
        // End Set
        // End Property

        #endregion

        /// <summary>
    /// Create a PepXML file using the peptides in file strInputFilePath
    /// </summary>
    /// <param name="strInputFilePath"></param>
    /// <param name="strOutputFolderPath"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        public bool ConvertPHRPDataToXML(string strInputFilePath, string strOutputFolderPath)
        {
            PHRPReader.Data.SearchEngineParameters objSearchEngineParams = null;
            string strOutputFilePath;
            bool blnSuccess;

            // Note that CachePHRPData() will update these variables
            mDatasetName = "Unknown";
            mPeptideHitResultType = PHRPReader.PeptideHitResultTypes.Unknown;
            blnSuccess = CachePHRPData(strInputFilePath, ref objSearchEngineParams);
            if (!blnSuccess)
                return false;
            if (mPreviewMode)
            {
                PreviewRequiredFiles(strInputFilePath, mDatasetName, mPeptideHitResultType, mLoadModsAndSeqInfo, mLoadMSGFResults, mLoadScanStats, mSearchEngineParamFileName);
            }
            else
            {
                strOutputFilePath = Path.Combine(strOutputFolderPath, mDatasetName + ".pepXML");
                blnSuccess = WriteCachedData(strInputFilePath, strOutputFilePath, ref objSearchEngineParams);
            }

            return blnSuccess;
        }

        protected bool CachePHRPData(string strInputFilePath, ref PHRPReader.Data.SearchEngineParameters objSearchEngineParams)
        {
            bool blnSuccess;
            clsPepXMLWriter.udtSpectrumInfoType udtSpectrumInfo;
            bool blnSkipPeptide;
            SortedSet<string> lstPeptidesToFilterOn;
            List<PHRPReader.Data.PSM> objPSMs = null;

            // Keys in this dictionary are scan numbers
            Dictionary<int, clsPSMInfo> dctBestPSMByScan;
            int intPeptidesStored;
            try
            {
                if (mPreviewMode)
                {
                    ResetProgress("Finding required files");
                }
                else
                {
                    ResetProgress("Caching PHRP data");
                }

                if (mPSMsBySpectrumKey is null)
                {
                    mPSMsBySpectrumKey = new Dictionary<string, List<PHRPReader.Data.PSM>>();
                }
                else
                {
                    mPSMsBySpectrumKey.Clear();
                }

                if (mSpectrumInfo is null)
                {
                    mSpectrumInfo = new Dictionary<string, clsPepXMLWriter.udtSpectrumInfoType>();
                }
                else
                {
                    mSpectrumInfo.Clear();
                }

                dctBestPSMByScan = new Dictionary<int, clsPSMInfo>();
                if (mChargeFilterList is null)
                    mChargeFilterList = new List<int>();
                intPeptidesStored = 0;
                var oStartupOptions = new PHRPReader.StartupOptions()
                {
                    LoadModsAndSeqInfo = mLoadModsAndSeqInfo,
                    LoadMSGFResults = mLoadMSGFResults,
                    LoadScanStatsData = mLoadScanStats,
                    MaxProteinsPerPSM = MaxProteinsPerPSM
                };
                mPHRPReader = new PHRPReader.ReaderFactory(strInputFilePath, oStartupOptions);
                RegisterEvents(mPHRPReader);
                mDatasetName = mPHRPReader.DatasetName;
                mPeptideHitResultType = mPHRPReader.PeptideHitResultType;
                mSeqToProteinMapCached = mPHRPReader.SeqToProteinMap;
                lstPeptidesToFilterOn = new SortedSet<string>();
                if (!string.IsNullOrWhiteSpace(mPeptideFilterFilePath))
                {
                    blnSuccess = LoadPeptideFilterFile(mPeptideFilterFilePath, ref lstPeptidesToFilterOn);
                    if (!blnSuccess)
                        return false;
                }

                if (mPreviewMode)
                {
                    // We can exit this function now since we have determined the dataset name and peptide hit result type
                    return true;
                }

                // Report any errors cached during instantiation of mPHRPReader
                foreach (var strMessage in mPHRPReader.ErrorMessages.Distinct())
                    ShowErrorMessage(strMessage);

                // Report any warnings cached during instantiation of mPHRPReader
                foreach (var strMessage in mPHRPReader.WarningMessages.Distinct())
                {
                    Console.WriteLine();
                    ShowWarningMessage(strMessage);
                    if (strMessage.Contains("SeqInfo file not found"))
                    {
                        if (mPHRPReader.ModSummaryFileLoaded)
                        {
                            ShowMessage("  ... will use the ModSummary file to infer the peptide modifications");
                        }
                        else
                        {
                            ShowMessage("  ... use the /NoMods switch to avoid this error (though modified peptides in that case modified peptides would not be stored properly)");
                        }
                    }
                    else if (strMessage.Contains("MSGF file not found"))
                    {
                        ShowMessage("  ... use the /NoMSGF switch to avoid this error");
                    }
                    else if (strMessage.Contains("Extended ScanStats file not found"))
                    {
                        ShowMessage("  ... parent ion m/z values may not be completely accurate; use the /NoScanStats switch to avoid this error");
                    }
                    else if (strMessage.Contains("ScanStats file not found"))
                    {
                        ShowMessage("  ... unable to determine elution times; use the /NoScanStats switch to avoid this error");
                    }
                    else if (strMessage.Contains("ModSummary file not found"))
                    {
                        ShowMessage("  ... ModSummary file was not found; will infer modification details");
                    }
                }

                if (mPHRPReader.WarningMessages.Count > 0)
                    Console.WriteLine();
                mPHRPReader.ClearErrors();
                mPHRPReader.ClearWarnings();
                if (string.IsNullOrEmpty(mDatasetName))
                {
                    mDatasetName = "Unknown";
                    ShowWarningMessage("Unable to determine the dataset name from the input file path; database will be named " + mDatasetName + " in the PepXML file");
                }

                while (mPHRPReader.MoveNext())
                {
                    var objCurrentPSM = mPHRPReader.CurrentPSM;
                    if (mSkipXPeptides && objCurrentPSM.PeptideCleanSequence.Contains("X"))
                    {
                        blnSkipPeptide = true;
                    }
                    else
                    {
                        blnSkipPeptide = false;
                    }

                    if (!blnSkipPeptide && mHitsPerSpectrum > 0)
                    {
                        if (objCurrentPSM.ScoreRank > mHitsPerSpectrum)
                        {
                            blnSkipPeptide = true;
                        }
                    }

                    if (!blnSkipPeptide && lstPeptidesToFilterOn.Count > 0)
                    {
                        if (!lstPeptidesToFilterOn.Contains(objCurrentPSM.PeptideCleanSequence))
                        {
                            blnSkipPeptide = true;
                        }
                    }

                    if (!blnSkipPeptide && mChargeFilterList.Count > 0)
                    {
                        if (!mChargeFilterList.Contains(objCurrentPSM.Charge))
                        {
                            blnSkipPeptide = true;
                        }
                    }

                    if (!blnSkipPeptide)
                    {
                        string strSpectrumKey;
                        strSpectrumKey = GetSpectrumKey(ref objCurrentPSM);
                        if (!mSpectrumInfo.ContainsKey(strSpectrumKey))
                        {
                            // New spectrum; add a new entry to mSpectrumInfo
                            udtSpectrumInfo = new clsPepXMLWriter.udtSpectrumInfoType();
                            udtSpectrumInfo.SpectrumName = strSpectrumKey;
                            udtSpectrumInfo.StartScan = objCurrentPSM.ScanNumberStart;
                            udtSpectrumInfo.EndScan = objCurrentPSM.ScanNumberEnd;
                            udtSpectrumInfo.PrecursorNeutralMass = objCurrentPSM.PrecursorNeutralMass;
                            udtSpectrumInfo.AssumedCharge = objCurrentPSM.Charge;
                            udtSpectrumInfo.ElutionTimeMinutes = objCurrentPSM.ElutionTimeMinutes;
                            udtSpectrumInfo.CollisionMode = objCurrentPSM.CollisionMode;
                            udtSpectrumInfo.Index = mSpectrumInfo.Count;
                            udtSpectrumInfo.NativeID = ConstructNativeID(objCurrentPSM.ScanNumberStart);
                            mSpectrumInfo.Add(strSpectrumKey, udtSpectrumInfo);
                        }

                        if (mPSMsBySpectrumKey.TryGetValue(strSpectrumKey, out objPSMs))
                        {
                            objPSMs.Add(objCurrentPSM);
                        }
                        else
                        {
                            objPSMs = new List<PHRPReader.Data.PSM>() { objCurrentPSM };
                            mPSMsBySpectrumKey.Add(strSpectrumKey, objPSMs);
                        }

                        if (mTopHitOnly)
                        {
                            clsPSMInfo objBestPSMInfo = null;
                            var objComparisonPSMInfo = new clsPSMInfo(strSpectrumKey, objCurrentPSM);
                            if (dctBestPSMByScan.TryGetValue(objCurrentPSM.ScanNumberStart, out objBestPSMInfo))
                            {
                                if (objComparisonPSMInfo.MSGFSpecProb < objBestPSMInfo.MSGFSpecProb)
                                {
                                    // We have found a better scoring peptide for this scan
                                    dctBestPSMByScan[objCurrentPSM.ScanNumberStart] = objComparisonPSMInfo;
                                }
                            }
                            else
                            {
                                dctBestPSMByScan.Add(objCurrentPSM.ScanNumberStart, objComparisonPSMInfo);
                            }
                        }

                        intPeptidesStored += 1;
                    }

                    UpdateProgress(mPHRPReader.PercentComplete);
                }

                OperationComplete();
                Console.WriteLine();
                string strFilterMessage = string.Empty;
                if (lstPeptidesToFilterOn.Count > 0)
                {
                    strFilterMessage = " (filtered using " + lstPeptidesToFilterOn.Count + " peptides in " + Path.GetFileName(mPeptideFilterFilePath) + ")";
                }

                if (mTopHitOnly)
                {
                    // Update mPSMsBySpectrumKey to contain the best hit for each scan number (regardless of charge)

                    int intCountAtStart = mPSMsBySpectrumKey.Count;
                    mPSMsBySpectrumKey.Clear();
                    foreach (var item in dctBestPSMByScan)
                    {
                        var lstPSMs = new List<PHRPReader.Data.PSM>() { item.Value.PSM };
                        mPSMsBySpectrumKey.Add(item.Value.SpectrumKey, lstPSMs);
                    }

                    intPeptidesStored = mPSMsBySpectrumKey.Count;
                    ShowMessage(" ... cached " + intPeptidesStored.ToString("#,##0") + " PSMs" + strFilterMessage);
                    ShowMessage(" ... filtered out " + (intCountAtStart - intPeptidesStored).ToString("#,##0") + " PSMs to only retain the top hit for each scan (regardless of charge)");
                }
                else
                {
                    ShowMessage(" ... cached " + intPeptidesStored.ToString("#,##0") + " PSMs" + strFilterMessage);
                }

                // Load the search engine parameters
                objSearchEngineParams = LoadSearchEngineParameters(ref mPHRPReader, mSearchEngineParamFileName);
                blnSuccess = true;
            }
            catch (Exception ex)
            {
                if (mPreviewMode)
                {
                    Console.WriteLine();
                    ShowMessage("Unable to preview the required files since not able to determine the dataset name: " + ex.Message);
                }
                else
                {
                    ShowErrorMessage("Error Reading source file in CachePHRPData: " + ex.Message);
                    if (ex.Message.Contains("ModSummary file not found"))
                    {
                        SetLocalErrorCode(PeptideListToXMLErrorCodes.ModSummaryFileNotFound);
                    }
                    else
                    {
                        SetLocalErrorCode(PeptideListToXMLErrorCodes.ErrorReadingInputFile);
                        ShowMessage(ex.StackTrace);
                    }
                }

                blnSuccess = false;
            }

            return blnSuccess;
        }

        /// <summary>
    /// Constructs a Thermo-style nativeID string for the given spectrum
    /// This allows for linking up with data in .mzML files
    /// </summary>
    /// <param name="intScanNumber"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        protected string ConstructNativeID(int intScanNumber)
        {
            // Examples:
            // Most Thermo raw files: "controllerType=0 controllerNumber=1 scan=6"
            // Thermo raw with PQD spectra: "controllerType=1 controllerNumber=1 scan=6"
            // Wiff files: "sample=1 period=1 cycle=123 experiment=2"
            // Waters files: "function=2 process=0 scan=123

            // For now, we're assuming all data processed by this program is from Thermo raw files

            return "controllerType=0 controllerNumber=1 scan=" + intScanNumber.ToString();
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
    /// Returns the default file extensions that this class knows how to parse
    /// </summary>
    /// <returns></returns>
    /// <remarks></remarks>
        public override IList<string> GetDefaultExtensionsToParse()
        {
            var extensionsToParse = new List<string>() { PHRPReader.Reader.SequestSynFileReader.GetPHRPSynopsisFileName(string.Empty), PHRPReader.Reader.XTandemSynFileReader.GetPHRPSynopsisFileName(string.Empty), PHRPReader.Reader.MSGFPlusSynFileReader.GetPHRPSynopsisFileName(string.Empty), PHRPReader.Reader.InspectSynFileReader.GetPHRPSynopsisFileName(string.Empty), PHRPReader.Reader.MODaSynFileReader.GetPHRPSynopsisFileName(string.Empty), PHRPReader.Reader.MODPlusSynFileReader.GetPHRPSynopsisFileName(string.Empty) };
            return extensionsToParse;
        }

        /// <summary>
    /// Returns the error message; empty string if no error
    /// </summary>
    /// <returns></returns>
    /// <remarks></remarks>
        public override string GetErrorMessage()
        {
            string strErrorMessage;
            if (ErrorCode == ProcessFilesErrorCodes.LocalizedError | ErrorCode == ProcessFilesErrorCodes.NoError)
            {
                switch (mLocalErrorCode)
                {
                    case PeptideListToXMLErrorCodes.NoError:
                        {
                            strErrorMessage = "";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.ErrorReadingInputFile:
                        {
                            strErrorMessage = "Error reading input file";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.ErrorWritingOutputFile:
                        {
                            strErrorMessage = "Error writing to the output file";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.ModSummaryFileNotFound:
                        {
                            strErrorMessage = "ModSummary file not found; use the /NoMods switch to avoid this error (though modified peptides will not be stored properly)";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.SeqInfoFileNotFound:
                        {
                            strErrorMessage = "SeqInfo file not found; use the /NoMods switch to avoid this error (though modified peptides will not be stored properly)";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.MSGFStatsFileNotFound:
                        {
                            strErrorMessage = "MSGF file not found; use the /NoMSGF switch to avoid this error";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.ScanStatsFileNotFound:
                        {
                            strErrorMessage = "MASIC ScanStats file not found; use the /NoScanStats switch to avoid this error";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.UnspecifiedError:
                        {
                            strErrorMessage = "Unspecified localized error";
                            break;
                        }

                    default:
                        {
                            // This shouldn't happen
                            strErrorMessage = "Unknown error state";
                            break;
                        }
                }
            }
            else
            {
                strErrorMessage = GetBaseClassErrorMessage();
            }

            return strErrorMessage;
        }

        protected string GetSpectrumKey(ref PHRPReader.Data.PSM CurrentPSM)
        {
            return mDatasetName + "." + CurrentPSM.ScanNumberStart + "." + CurrentPSM.ScanNumberEnd + "." + CurrentPSM.Charge;
        }

        private void InitializeLocalVariables()
        {
            mLocalErrorCode = PeptideListToXMLErrorCodes.NoError;
            mDatasetName = "Unknown";
            mPeptideHitResultType = PHRPReader.PeptideHitResultTypes.Unknown;
            mFastaFilePath = string.Empty;
            mSearchEngineParamFileName = string.Empty;
            mHitsPerSpectrum = DEFAULT_HITS_PER_SPECTRUM;
            mSkipXPeptides = false;
            mTopHitOnly = false;
            mMaxProteinsPerPSM = DEFAULT_MAX_PROTEINS_PER_PSM;
            mPeptideFilterFilePath = string.Empty;
            mChargeFilterList = new List<int>();
            mLoadModsAndSeqInfo = true;
            mLoadMSGFResults = true;
            mLoadScanStats = true;
        }

        /// <summary>
    /// Loads the settings from the parameter file
    /// </summary>
    /// <param name="strParameterFilePath"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        public bool LoadParameterFileSettings(string strParameterFilePath)
        {
            var objSettingsFile = new XmlSettingsFileAccessor();
            try
            {
                if (strParameterFilePath is null || strParameterFilePath.Length == 0)
                {
                    // No parameter file specified; nothing to load
                    return true;
                }

                if (!File.Exists(strParameterFilePath))
                {
                    // See if strParameterFilePath points to a file in the same directory as the application
                    strParameterFilePath = Path.Combine(GetAppDirectoryPath(), Path.GetFileName(strParameterFilePath));
                    if (!File.Exists(strParameterFilePath))
                    {
                        SetBaseClassErrorCode(ProcessFilesErrorCodes.ParameterFileNotFound);
                        return false;
                    }
                }

                if (objSettingsFile.LoadSettings(strParameterFilePath))
                {
                    if (!objSettingsFile.SectionPresent(XML_SECTION_OPTIONS))
                    {
                        ShowErrorMessage("The node '<section name=\"" + XML_SECTION_OPTIONS + "\"> was not found in the parameter file: " + strParameterFilePath);
                        SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile);
                        return false;
                    }
                    else
                    {
                        FastaFilePath = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "FastaFilePath", FastaFilePath);
                        SearchEngineParamFileName = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "SearchEngineParamFileName", SearchEngineParamFileName);
                        HitsPerSpectrum = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "HitsPerSpectrum", HitsPerSpectrum);
                        SkipXPeptides = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "SkipXPeptides", SkipXPeptides);
                        TopHitOnly = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "TopHitOnly", TopHitOnly);
                        MaxProteinsPerPSM = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "MaxProteinsPerPSM", MaxProteinsPerPSM);
                        LoadModsAndSeqInfo = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "LoadModsAndSeqInfo", LoadModsAndSeqInfo);
                        LoadMSGFResults = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "LoadMSGFResults", LoadMSGFResults);
                        LoadScanStats = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "LoadScanStats", LoadScanStats);
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in LoadParameterFileSettings", ex);
                return false;
            }

            return true;
        }

        protected bool LoadPeptideFilterFile(string strInputFilePath, ref SortedSet<string> lstPeptides)
        {
            try
            {
                if (lstPeptides is null)
                {
                    lstPeptides = new SortedSet<string>();
                }
                else
                {
                    lstPeptides.Clear();
                }

                if (!File.Exists(strInputFilePath))
                {
                    ShowErrorMessage("Peptide filter file not found: " + strInputFilePath);
                    SetLocalErrorCode(PeptideListToXMLErrorCodes.ErrorReadingInputFile);
                    return false;
                }

                if (mPreviewMode)
                {
                    ShowMessage("Peptide Filter file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetMSGFFileName(strInputFilePath));
                    return true;
                }

                using (var reader = new StreamReader(new FileStream(strInputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    while (!reader.EndOfStream)
                    {
                        string dataLine = reader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(dataLine))
                        {
                            // Remove any text present after a tab character
                            int tabIndex = dataLine.IndexOf('\t');
                            if (tabIndex > 0)
                            {
                                dataLine = dataLine.Substring(0, tabIndex);
                            }

                            if (!string.IsNullOrWhiteSpace(dataLine))
                            {
                                dataLine = PHRPReader.PeptideCleavageStateCalculator.ExtractCleanSequenceFromSequenceWithMods(dataLine, true);
                                lstPeptides.Add(dataLine);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in LoadPeptideFilterFile", ex);
                return false;
            }

            return true;
        }

        protected PHRPReader.Data.SearchEngineParameters LoadSearchEngineParameters(ref PHRPReader.ReaderFactory reader, string strSearchEngineParamFileName)
        {
            PHRPReader.Data.SearchEngineParameters objSearchEngineParams = null;
            bool blnSuccess;
            try
            {
                Console.WriteLine();
                if (string.IsNullOrEmpty(strSearchEngineParamFileName))
                {
                    ShowWarningMessage("Search engine parameter file not defined; use /E to specify the filename");
                    objSearchEngineParams = new PHRPReader.Data.SearchEngineParameters(mPeptideHitResultType.ToString());
                }
                else
                {
                    ShowMessage("Loading Search Engine parameters");
                    blnSuccess = reader.SynFileReader.LoadSearchEngineParameters(strSearchEngineParamFileName, out objSearchEngineParams);
                    if (!blnSuccess)
                    {
                        objSearchEngineParams = new PHRPReader.Data.SearchEngineParameters(mPeptideHitResultType.ToString());
                    }
                }

                // Make sure mSearchEngineParams.ModInfo is up-to-date

                string strSpectrumKey;
                var udtCurrentSpectrum = new clsPepXMLWriter.udtSpectrumInfoType();
                bool blnMatchFound;
                foreach (var objItem in mPSMsBySpectrumKey)
                {
                    strSpectrumKey = objItem.Key;
                    if (mSpectrumInfo.TryGetValue(strSpectrumKey, out udtCurrentSpectrum))
                    {
                        foreach (PHRPReader.Data.PSM oPSMEntry in objItem.Value)
                        {
                            if (oPSMEntry.ModifiedResidues.Count > 0)
                            {
                                foreach (PHRPReader.Data.AminoAcidModInfo objResidue in oPSMEntry.ModifiedResidues)
                                {

                                    // Check whether objResidue.ModDefinition is present in objSearchEngineParams.ModInfo
                                    blnMatchFound = false;
                                    foreach (PHRPReader.Data.ModificationDefinition objKnownMod in objSearchEngineParams.ModList)
                                    {
                                        if (objKnownMod.EquivalentMassTypeTagAtomAndResidues(objResidue.ModDefinition))
                                        {
                                            blnMatchFound = true;
                                            break;
                                        }
                                    }

                                    if (!blnMatchFound)
                                    {
                                        objSearchEngineParams.ModList.Add(objResidue.ModDefinition);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in LoadSearchEngineParameters", ex);
            }

            return objSearchEngineParams;
        }

        protected void PreviewRequiredFiles(string strInputFilePath, string strDatasetName, PHRPReader.PeptideHitResultTypes PeptideHitResultTypes, bool blnLoadModsAndSeqInfo, bool blnLoadMSGFResults, bool blnLoadScanStats, string strSearchEngineParamFileName)
        {
            var fiFileInfo = new FileInfo(strInputFilePath);
            Console.WriteLine();
            if (fiFileInfo.DirectoryName.Length > 40)
            {
                ShowMessage("Data file folder: ");
                ShowMessage(fiFileInfo.DirectoryName);
                Console.WriteLine();
            }
            else
            {
                ShowMessage("Data file folder: " + fiFileInfo.DirectoryName);
            }

            ShowMessage("Data file: ".PadRight(PREVIEW_PAD_WIDTH) + Path.GetFileName(strInputFilePath));
            if (blnLoadModsAndSeqInfo)
            {
                ShowMessage("ModSummary file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetPHRPModSummaryFileName(PeptideHitResultTypes, strDatasetName));
                if ((fiFileInfo.Name.ToLower() ?? "") == (PHRPReader.ReaderFactory.GetPHRPSynopsisFileName(PeptideHitResultTypes, strDatasetName).ToLower() ?? ""))
                {
                    ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetPHRPResultToSeqMapFileName(PeptideHitResultTypes, strDatasetName));
                    ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetPHRPSeqInfoFileName(PeptideHitResultTypes, strDatasetName));
                    ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetPHRPSeqToProteinMapFileName(PeptideHitResultTypes, strDatasetName));
                }
            }

            if (blnLoadMSGFResults)
            {
                ShowMessage("MSGF Results file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetMSGFFileName(strInputFilePath));
            }

            if (blnLoadScanStats)
            {
                ShowMessage("ScanStats file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetScanStatsFilename(mDatasetName));
                ShowMessage("ScanStats file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetExtendedScanStatsFilename(mDatasetName));
            }

            if (!string.IsNullOrEmpty(strSearchEngineParamFileName))
            {
                ShowMessage("Search Engine Params: ".PadRight(PREVIEW_PAD_WIDTH) + strSearchEngineParamFileName);
                ShowMessage("Tool Version File: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetToolVersionInfoFilenames(PeptideHitResultTypes).First());
                if (mPeptideHitResultType == PHRPReader.PeptideHitResultTypes.XTandem)
                {
                    // Determine the additional files that will be required
                    List<string> lstFileNames;
                    lstFileNames = PHRPReader.Reader.XTandemSynFileReader.GetAdditionalSearchEngineParamFileNames(Path.Combine(fiFileInfo.DirectoryName, strSearchEngineParamFileName));
                    foreach (var strFileName in lstFileNames)
                        ShowMessage("Search Engine Params: ".PadRight(PREVIEW_PAD_WIDTH) + strFileName);
                }
            }
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
    /// Main processing function; calls ConvertPHRPDataToXML
    /// </summary>
    /// <param name="strInputFilePath">PHRP Input file path</param>
    /// <param name="strOutputFolderPath">Output folder path (if empty, then output file will be created in the same folder as the input file)</param>
    /// <param name="strParameterFilePath">Parameter file path</param>
    /// <param name="blnResetErrorCode">True to reset the error code prior to processing</param>
    /// <returns></returns>
    /// <remarks></remarks>
        public override bool ProcessFile(string strInputFilePath, string strOutputFolderPath, string strParameterFilePath, bool blnResetErrorCode)
        {
            // Returns True if success, False if failure

            FileInfo ioFile;
            string strInputFilePathFull;
            var blnSuccess = default(bool);
            if (blnResetErrorCode)
            {
                SetLocalErrorCode(PeptideListToXMLErrorCodes.NoError);
            }

            if (!LoadParameterFileSettings(strParameterFilePath))
            {
                ShowErrorMessage("Parameter file load error: " + strParameterFilePath);
                if (ErrorCode == ProcessFilesErrorCodes.NoError)
                {
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile);
                }

                return false;
            }

            try
            {
                if (strInputFilePath is null || strInputFilePath.Length == 0)
                {
                    ShowMessage("Input file name is empty");
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath);
                }
                else
                {
                    Console.WriteLine();
                    if (!mPreviewMode)
                    {
                        ShowMessage("Parsing " + Path.GetFileName(strInputFilePath));
                    }

                    // Note that CleanupFilePaths() will update mOutputFolderPath, which is used by LogMessage()
                    if (!CleanupFilePaths(ref strInputFilePath, ref strOutputFolderPath))
                    {
                        SetBaseClassErrorCode(ProcessFilesErrorCodes.FilePathError);
                    }
                    else
                    {
                        if (!mPreviewMode)
                        {
                            ResetProgress();
                        }

                        try
                        {
                            // Obtain the full path to the input file
                            ioFile = new FileInfo(strInputFilePath);
                            strInputFilePathFull = ioFile.FullName;
                            blnSuccess = ConvertPHRPDataToXML(strInputFilePathFull, strOutputFolderPath);
                        }
                        catch (Exception ex)
                        {
                            HandleException("Error calling ConvertToXML", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessFile", ex);
            }

            return blnSuccess;
        }

        protected void ShowWarningMessage(string strWarningMessage)
        {
            ShowMessage("Warning: " + strWarningMessage);
        }

        private void SetLocalErrorCode(PeptideListToXMLErrorCodes newErrorCode)
        {
            SetLocalErrorCode(newErrorCode, false);
        }

        private void SetLocalErrorCode(PeptideListToXMLErrorCodes newErrorCode, bool blnLeaveExistingErrorCodeUnchanged)
        {
            if (blnLeaveExistingErrorCodeUnchanged && mLocalErrorCode != PeptideListToXMLErrorCodes.NoError)
            {
            }
            // An error code is already defined; do not change it
            else
            {
                mLocalErrorCode = newErrorCode;
                if (newErrorCode == PeptideListToXMLErrorCodes.NoError)
                {
                    if (ErrorCode == ProcessFilesErrorCodes.LocalizedError)
                    {
                        SetBaseClassErrorCode(ProcessFilesErrorCodes.NoError);
                    }
                }
                else
                {
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.LocalizedError);
                }
            }
        }

        protected bool WriteCachedData(string strInputFilePath, string strOutputFilePath, ref PHRPReader.Data.SearchEngineParameters objSearchEngineParams)
        {
            int intSpectra;
            int intPeptides;
            bool blnSuccess;
            string strSpectrumKey;
            var udtCurrentSpectrum = new clsPepXMLWriter.udtSpectrumInfoType();
            ResetProgress("Creating the .pepXML file");
            try
            {
                Console.WriteLine();
                ShowMessage("Creating PepXML file at " + Path.GetFileName(strOutputFilePath));
                mXMLWriter = new clsPepXMLWriter(mDatasetName, mFastaFilePath, objSearchEngineParams, strInputFilePath, strOutputFilePath);
                RegisterEvents(mXMLWriter);
                if (!mXMLWriter.IsWritable)
                {
                    ShowErrorMessage("XMLWriter is not writable; aborting");
                    return false;
                }

                mXMLWriter.MaxProteinsPerPSM = mMaxProteinsPerPSM;
                intSpectra = 0;
                intPeptides = 0;

                foreach (var spectrumKey in mPSMsBySpectrumKey.Keys)
                {
                    var psm = mPSMsBySpectrumKey[spectrumKey];

                    if (mSpectrumInfo.TryGetValue(spectrumKey, out udtCurrentSpectrum))
                    {
                        intSpectra += 1;
                        intPeptides += psm.Count;

                        mXMLWriter.WriteSpectrum(ref udtCurrentSpectrum, psm, ref mSeqToProteinMapCached);
                    }
                    else
                    {
                        ShowErrorMessage("Spectrum key '" + spectrumKey + "' not found in mSpectrumInfo; this is unexpected");
                    }

                    float pctComplete = intSpectra / (float)mPSMsBySpectrumKey.Count * 100f;
                    UpdateProgress(pctComplete);
                }

                if (intPeptides > 500)
                {
                    Console.WriteLine();
                }

                mXMLWriter.CloseDocument();
                Console.WriteLine();
                ShowMessage("PepXML file created with " + intSpectra.ToString("#,##0") + " spectra and " + intPeptides.ToString("#,##0") + " peptides");
                blnSuccess = true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error Reading source file in WriteCachedData: " + ex.Message);
                blnSuccess = false;
            }

            return blnSuccess;
        }
    }
}