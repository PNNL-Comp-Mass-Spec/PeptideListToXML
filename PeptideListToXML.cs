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
    public class PeptideListToXML : PRISM.FileProcessor.ProcessFilesBase
    {
        // Ignore Spelling: mzIdentML, Wiff

        public PeptideListToXML()
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
        protected PepXMLWriter mXMLWriter;

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
        protected Dictionary<string, PepXMLWriter.SpectrumInfoType> mSpectrumInfo;
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
        /// Create a PepXML file using the peptides in file inputFilePath
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="outputFolderPath"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool ConvertPHRPDataToXML(string inputFilePath, string outputFolderPath)
        {
            string outputFilePath;
            bool success;

            // Note that CachePHRPData() will update these variables
            mDatasetName = "Unknown";
            mPeptideHitResultType = PHRPReader.PeptideHitResultTypes.Unknown;
            success = CachePHRPData(inputFilePath, out var searchEngineParams);

            if (!success)
                return false;

            if (mPreviewMode)
            {
                PreviewRequiredFiles(inputFilePath, mDatasetName, mPeptideHitResultType, mLoadModsAndSeqInfo, mLoadMSGFResults, mLoadScanStats, mSearchEngineParamFileName);
            }
            else
            {
                outputFilePath = Path.Combine(outputFolderPath, mDatasetName + ".pepXML");
                success = WriteCachedData(inputFilePath, outputFilePath, searchEngineParams);
            }

            return success;
        }

        protected bool CachePHRPData(string inputFilePath, out PHRPReader.Data.SearchEngineParameters searchEngineParams)
        {
            bool success;
            PepXMLWriter.SpectrumInfoType spectrumInfo;
            bool skipPeptide;

            // Keys in this dictionary are scan numbers
            Dictionary<int, PSMInfo> bestPSMByScan;
            int peptidesStored;
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
                    mSpectrumInfo = new Dictionary<string, PepXMLWriter.SpectrumInfoType>();
                }
                else
                {
                    mSpectrumInfo.Clear();
                }

                bestPSMByScan = new Dictionary<int, PSMInfo>();
                if (mChargeFilterList is null)
                    mChargeFilterList = new List<int>();

                peptidesStored = 0;
                var startupOptions = new PHRPReader.StartupOptions()
                {
                    LoadModsAndSeqInfo = mLoadModsAndSeqInfo,
                    LoadMSGFResults = mLoadMSGFResults,
                    LoadScanStatsData = mLoadScanStats,
                    MaxProteinsPerPSM = MaxProteinsPerPSM
                };

                mPHRPReader = new PHRPReader.ReaderFactory(inputFilePath, startupOptions);
                RegisterEvents(mPHRPReader);

                mDatasetName = mPHRPReader.DatasetName;
                mPeptideHitResultType = mPHRPReader.PeptideHitResultType;
                mSeqToProteinMapCached = mPHRPReader.SeqToProteinMap;

                SortedSet<string> peptidesToFilterOn;
                if (!string.IsNullOrWhiteSpace(mPeptideFilterFilePath))
                {
                    success = LoadPeptideFilterFile(mPeptideFilterFilePath, out peptidesToFilterOn);
                    if (!success)
                    {
                        searchEngineParams = new PHRPReader.Data.SearchEngineParameters(string.Empty);
                        return false;
                    }
                }
                else
                {
                    peptidesToFilterOn = new SortedSet<string>();
                }

                if (mPreviewMode)
                {
                    // We can exit this function now since we have determined the dataset name and peptide hit result type
                    searchEngineParams = new PHRPReader.Data.SearchEngineParameters(string.Empty);
                    return true;
                }

                // Report any errors cached during instantiation of mPHRPReader
                foreach (var message in mPHRPReader.ErrorMessages.Distinct())
                    ShowErrorMessage(message);

                // Report any warnings cached during instantiation of mPHRPReader
                foreach (var message in mPHRPReader.WarningMessages.Distinct())
                {
                    Console.WriteLine();
                    ShowWarningMessage(message);
                    if (message.Contains("SeqInfo file not found"))
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
                    else if (message.Contains("MSGF file not found"))
                    {
                        ShowMessage("  ... use the /NoMSGF switch to avoid this error");
                    }
                    else if (message.Contains("Extended ScanStats file not found"))
                    {
                        ShowMessage("  ... parent ion m/z values may not be completely accurate; use the /NoScanStats switch to avoid this error");
                    }
                    else if (message.Contains("ScanStats file not found"))
                    {
                        ShowMessage("  ... unable to determine elution times; use the /NoScanStats switch to avoid this error");
                    }
                    else if (message.Contains("ModSummary file not found"))
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
                    var currentPSM = mPHRPReader.CurrentPSM;
                    if (mSkipXPeptides && currentPSM.PeptideCleanSequence.Contains("X"))
                    {
                        skipPeptide = true;
                    }
                    else
                    {
                        skipPeptide = false;
                    }

                    if (!skipPeptide && mHitsPerSpectrum > 0)
                    {
                        if (currentPSM.ScoreRank > mHitsPerSpectrum)
                        {
                            skipPeptide = true;
                        }
                    }

                    if (!skipPeptide && peptidesToFilterOn.Count > 0)
                    {
                        if (!peptidesToFilterOn.Contains(currentPSM.PeptideCleanSequence))
                        {
                            skipPeptide = true;
                        }
                    }

                    if (!skipPeptide && mChargeFilterList.Count > 0)
                    {
                        if (!mChargeFilterList.Contains(currentPSM.Charge))
                        {
                            skipPeptide = true;
                        }
                    }

                    if (!skipPeptide)
                    {
                        var spectrumKey = GetSpectrumKey(currentPSM);
                        if (!mSpectrumInfo.ContainsKey(spectrumKey))
                        {
                            // New spectrum; add a new entry to mSpectrumInfo
                            spectrumInfo = new PepXMLWriter.SpectrumInfoType();
                            spectrumInfo.SpectrumName = spectrumKey;
                            spectrumInfo.StartScan = currentPSM.ScanNumberStart;
                            spectrumInfo.EndScan = currentPSM.ScanNumberEnd;
                            spectrumInfo.PrecursorNeutralMass = currentPSM.PrecursorNeutralMass;
                            spectrumInfo.AssumedCharge = currentPSM.Charge;
                            spectrumInfo.ElutionTimeMinutes = currentPSM.ElutionTimeMinutes;
                            spectrumInfo.CollisionMode = currentPSM.CollisionMode;
                            spectrumInfo.Index = mSpectrumInfo.Count;
                            spectrumInfo.NativeID = ConstructNativeID(currentPSM.ScanNumberStart);
                            mSpectrumInfo.Add(spectrumKey, spectrumInfo);
                        }

                        if (mPSMsBySpectrumKey.TryGetValue(spectrumKey, out var psms))
                        {
                            psms.Add(currentPSM);
                        }
                        else
                        {
                            psms = new List<PHRPReader.Data.PSM>() { currentPSM };
                            mPSMsBySpectrumKey.Add(spectrumKey, psms);
                        }

                        if (mTopHitOnly)
                        {
                            var comparisonPSMInfo = new PSMInfo(spectrumKey, currentPSM);

                            if (bestPSMByScan.TryGetValue(currentPSM.ScanNumberStart, out var bestPSMInfo))
                            {
                                if (comparisonPSMInfo.MSGFSpecProb < bestPSMInfo.MSGFSpecProb)
                                {
                                    // We have found a better scoring peptide for this scan
                                    bestPSMByScan[currentPSM.ScanNumberStart] = comparisonPSMInfo;
                                }
                            }
                            else
                            {
                                bestPSMByScan.Add(currentPSM.ScanNumberStart, comparisonPSMInfo);
                            }
                        }

                        peptidesStored += 1;
                    }

                    UpdateProgress(mPHRPReader.PercentComplete);
                }

                OperationComplete();
                Console.WriteLine();
                string filterMessage = string.Empty;
                if (peptidesToFilterOn.Count > 0)
                {
                    filterMessage = " (filtered using " + peptidesToFilterOn.Count + " peptides in " + Path.GetFileName(mPeptideFilterFilePath) + ")";
                }

                if (mTopHitOnly)
                {
                    // Update mPSMsBySpectrumKey to contain the best hit for each scan number (regardless of charge)

                    int countAtStart = mPSMsBySpectrumKey.Count;
                    mPSMsBySpectrumKey.Clear();
                    foreach (var item in bestPSMByScan)
                    {
                        var psms = new List<PHRPReader.Data.PSM>() { item.Value.PSM };
                        mPSMsBySpectrumKey.Add(item.Value.SpectrumKey, psms);
                    }

                    peptidesStored = mPSMsBySpectrumKey.Count;
                    ShowMessage(" ... cached " + peptidesStored.ToString("#,##0") + " PSMs" + filterMessage);
                    ShowMessage(" ... filtered out " + (countAtStart - peptidesStored).ToString("#,##0") + " PSMs to only retain the top hit for each scan (regardless of charge)");
                }
                else
                {
                    ShowMessage(" ... cached " + peptidesStored.ToString("#,##0") + " PSMs" + filterMessage);
                }

                // Load the search engine parameters
                searchEngineParams = LoadSearchEngineParameters(mPHRPReader, mSearchEngineParamFileName);
                return true;
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

                searchEngineParams = new PHRPReader.Data.SearchEngineParameters(string.Empty);
                return false;
            }
        }

        /// <summary>
        /// Constructs a Thermo-style nativeID string for the given spectrum
        /// This allows for linking up with data in .mzML files
        /// </summary>
        /// <param name="scanNumber"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        protected string ConstructNativeID(int scanNumber)
        {
            // Examples:
            // Most Thermo raw files: "controllerType=0 controllerNumber=1 scan=6"
            // Thermo raw with PQD spectra: "controllerType=1 controllerNumber=1 scan=6"
            // Wiff files: "sample=1 period=1 cycle=123 experiment=2"
            // Waters files: "function=2 process=0 scan=123

            // For now, we're assuming all data processed by this program is from Thermo raw files

            return "controllerType=0 controllerNumber=1 scan=" + scanNumber.ToString();
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
            string errorMessage;
            if (ErrorCode == ProcessFilesErrorCodes.LocalizedError | ErrorCode == ProcessFilesErrorCodes.NoError)
            {
                switch (mLocalErrorCode)
                {
                    case PeptideListToXMLErrorCodes.NoError:
                        {
                            errorMessage = "";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.ErrorReadingInputFile:
                        {
                            errorMessage = "Error reading input file";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.ErrorWritingOutputFile:
                        {
                            errorMessage = "Error writing to the output file";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.ModSummaryFileNotFound:
                        {
                            errorMessage = "ModSummary file not found; use the /NoMods switch to avoid this error (though modified peptides will not be stored properly)";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.SeqInfoFileNotFound:
                        {
                            errorMessage = "SeqInfo file not found; use the /NoMods switch to avoid this error (though modified peptides will not be stored properly)";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.MSGFStatsFileNotFound:
                        {
                            errorMessage = "MSGF file not found; use the /NoMSGF switch to avoid this error";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.ScanStatsFileNotFound:
                        {
                            errorMessage = "MASIC ScanStats file not found; use the /NoScanStats switch to avoid this error";
                            break;
                        }

                    case PeptideListToXMLErrorCodes.UnspecifiedError:
                        {
                            errorMessage = "Unspecified localized error";
                            break;
                        }

                    default:
                        {
                            // This shouldn't happen
                            errorMessage = "Unknown error state";
                            break;
                        }
                }
            }
            else
            {
                errorMessage = GetBaseClassErrorMessage();
            }

            return errorMessage;
        }

        protected string GetSpectrumKey(PHRPReader.Data.PSM CurrentPSM)
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
        /// <param name="parameterFilePath"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool LoadParameterFileSettings(string parameterFilePath)
        {
            var settingsFile = new XmlSettingsFileAccessor();
            try
            {
                if (parameterFilePath is null || parameterFilePath.Length == 0)
                {
                    // No parameter file specified; nothing to load
                    return true;
                }

                if (!File.Exists(parameterFilePath))
                {
                    // See if parameterFilePath points to a file in the same directory as the application
                    parameterFilePath = Path.Combine(GetAppDirectoryPath(), Path.GetFileName(parameterFilePath));
                    if (!File.Exists(parameterFilePath))
                    {
                        SetBaseClassErrorCode(ProcessFilesErrorCodes.ParameterFileNotFound);
                        return false;
                    }
                }

                if (settingsFile.LoadSettings(parameterFilePath))
                {
                    if (!settingsFile.SectionPresent(XML_SECTION_OPTIONS))
                    {
                        ShowErrorMessage("The node '<section name=\"" + XML_SECTION_OPTIONS + "\"> was not found in the parameter file: " + parameterFilePath);
                        SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile);
                        return false;
                    }
                    else
                    {
                        FastaFilePath = settingsFile.GetParam(XML_SECTION_OPTIONS, "FastaFilePath", FastaFilePath);
                        SearchEngineParamFileName = settingsFile.GetParam(XML_SECTION_OPTIONS, "SearchEngineParamFileName", SearchEngineParamFileName);
                        HitsPerSpectrum = settingsFile.GetParam(XML_SECTION_OPTIONS, "HitsPerSpectrum", HitsPerSpectrum);
                        SkipXPeptides = settingsFile.GetParam(XML_SECTION_OPTIONS, "SkipXPeptides", SkipXPeptides);
                        TopHitOnly = settingsFile.GetParam(XML_SECTION_OPTIONS, "TopHitOnly", TopHitOnly);
                        MaxProteinsPerPSM = settingsFile.GetParam(XML_SECTION_OPTIONS, "MaxProteinsPerPSM", MaxProteinsPerPSM);
                        LoadModsAndSeqInfo = settingsFile.GetParam(XML_SECTION_OPTIONS, "LoadModsAndSeqInfo", LoadModsAndSeqInfo);
                        LoadMSGFResults = settingsFile.GetParam(XML_SECTION_OPTIONS, "LoadMSGFResults", LoadMSGFResults);
                        LoadScanStats = settingsFile.GetParam(XML_SECTION_OPTIONS, "LoadScanStats", LoadScanStats);
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

        protected bool LoadPeptideFilterFile(string inputFilePath, out SortedSet<string> peptides)
        {
            peptides = new SortedSet<string>();

            try
            {
                if (!File.Exists(inputFilePath))
                {
                    ShowErrorMessage("Peptide filter file not found: " + inputFilePath);
                    SetLocalErrorCode(PeptideListToXMLErrorCodes.ErrorReadingInputFile);
                    return false;
                }

                if (mPreviewMode)
                {
                    ShowMessage("Peptide Filter file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetMSGFFileName(inputFilePath));
                    return true;
                }

                using (var reader = new StreamReader(new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
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
                                peptides.Add(dataLine);
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

        protected PHRPReader.Data.SearchEngineParameters LoadSearchEngineParameters(PHRPReader.ReaderFactory reader, string searchEngineParamFileName)
        {
            PHRPReader.Data.SearchEngineParameters searchEngineParams = null;

            bool success;
            try
            {
                Console.WriteLine();
                if (string.IsNullOrEmpty(searchEngineParamFileName))
                {
                    ShowWarningMessage("Search engine parameter file not defined; use /E to specify the filename");
                    searchEngineParams = new PHRPReader.Data.SearchEngineParameters(mPeptideHitResultType.ToString());
                }
                else
                {
                    ShowMessage("Loading Search Engine parameters");
                    success = reader.SynFileReader.LoadSearchEngineParameters(searchEngineParamFileName, out searchEngineParams);
                    if (!success)
                    {
                        searchEngineParams = new PHRPReader.Data.SearchEngineParameters(mPeptideHitResultType.ToString());
                    }
                }

                // Make sure mSearchEngineParams.ModInfo is up-to-date

                string spectrumKey;
                var currentSpectrum = new PepXMLWriter.SpectrumInfoType();
                bool matchFound;
                foreach (var item in mPSMsBySpectrumKey)
                {
                    spectrumKey = item.Key;
                    if (mSpectrumInfo.TryGetValue(spectrumKey, out currentSpectrum))
                    {
                        foreach (PHRPReader.Data.PSM psmEntry in item.Value)
                        {
                            if (psmEntry.ModifiedResidues.Count > 0)
                            {
                                foreach (PHRPReader.Data.AminoAcidModInfo residue in psmEntry.ModifiedResidues)
                                {
                                    // Check whether residue.ModDefinition is present in searchEngineParams.ModInfo
                                    matchFound = false;
                                    foreach (PHRPReader.Data.ModificationDefinition knownMod in searchEngineParams.ModList)
                                    {
                                        if (knownMod.EquivalentMassTypeTagAtomAndResidues(residue.ModDefinition))
                                        {
                                            matchFound = true;
                                            break;
                                        }
                                    }

                                    if (!matchFound)
                                    {
                                        searchEngineParams.ModList.Add(residue.ModDefinition);
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

            return searchEngineParams;
        }

        protected void PreviewRequiredFiles(string inputFilePath, string datasetName, PHRPReader.PeptideHitResultTypes PeptideHitResultTypes, bool loadModsAndSeqInfo, bool loadMSGFResults, bool loadScanStats, string searchEngineParamFileName)
        {
            var inputFile = new FileInfo(inputFilePath);
            Console.WriteLine();
            if (inputFile.DirectoryName.Length > 40)
            {
                ShowMessage("Data file folder: ");
                ShowMessage(inputFile.DirectoryName);
                Console.WriteLine();
            }
            else
            {
                ShowMessage("Data file folder: " + inputFile.DirectoryName);
            }

            ShowMessage("Data file: ".PadRight(PREVIEW_PAD_WIDTH) + Path.GetFileName(inputFilePath));
            if (loadModsAndSeqInfo)
            {
                ShowMessage("ModSummary file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetPHRPModSummaryFileName(PeptideHitResultTypes, datasetName));
                if ((inputFile.Name.ToLower() ?? string.Empty) == (PHRPReader.ReaderFactory.GetPHRPSynopsisFileName(PeptideHitResultTypes, datasetName).ToLower() ?? string.Empty))
                {
                    ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetPHRPResultToSeqMapFileName(PeptideHitResultTypes, datasetName));
                    ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetPHRPSeqInfoFileName(PeptideHitResultTypes, datasetName));
                    ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetPHRPSeqToProteinMapFileName(PeptideHitResultTypes, datasetName));
                }
            }

            if (loadMSGFResults)
            {
                ShowMessage("MSGF Results file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetMSGFFileName(inputFilePath));
            }

            if (loadScanStats)
            {
                ShowMessage("ScanStats file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetScanStatsFilename(mDatasetName));
                ShowMessage("ScanStats file: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetExtendedScanStatsFilename(mDatasetName));
            }

            if (!string.IsNullOrEmpty(searchEngineParamFileName))
            {
                ShowMessage("Search Engine Params: ".PadRight(PREVIEW_PAD_WIDTH) + searchEngineParamFileName);
                ShowMessage("Tool Version File: ".PadRight(PREVIEW_PAD_WIDTH) + PHRPReader.ReaderFactory.GetToolVersionInfoFilenames(PeptideHitResultTypes).First());
                if (mPeptideHitResultType == PHRPReader.PeptideHitResultTypes.XTandem)
                {
                    // Determine the additional files that will be required
                    List<string> fileNames;
                    fileNames = PHRPReader.Reader.XTandemSynFileReader.GetAdditionalSearchEngineParamFileNames(Path.Combine(inputFile.DirectoryName, searchEngineParamFileName));
                    foreach (var fileName in fileNames)
                        ShowMessage("Search Engine Params: ".PadRight(PREVIEW_PAD_WIDTH) + fileName);
                }
            }
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
        /// Main processing function; calls ConvertPHRPDataToXML
        /// </summary>
        /// <param name="inputFilePath">PHRP Input file path</param>
        /// <param name="outputFolderPath">Output folder path (if empty, then output file will be created in the same folder as the input file)</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="resetErrorCode">True to reset the error code prior to processing</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public override bool ProcessFile(string inputFilePath, string outputFolderPath, string parameterFilePath, bool resetErrorCode)
        {
            // Returns True if success, False if failure

            string inputFilePathFull;
            var success = default(bool);
            if (resetErrorCode)
            {
                SetLocalErrorCode(PeptideListToXMLErrorCodes.NoError);
            }

            if (!LoadParameterFileSettings(parameterFilePath))
            {
                ShowErrorMessage("Parameter file load error: " + parameterFilePath);
                if (ErrorCode == ProcessFilesErrorCodes.NoError)
                {
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile);
                }

                return false;
            }

            try
            {
                if (inputFilePath is null || inputFilePath.Length == 0)
                {
                    ShowMessage("Input file name is empty");
                    SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath);
                }
                else
                {
                    Console.WriteLine();
                    if (!mPreviewMode)
                    {
                        ShowMessage("Parsing " + Path.GetFileName(inputFilePath));
                    }

                    // Note that CleanupFilePaths() will update mOutputFolderPath, which is used by LogMessage()
                    if (!CleanupFilePaths(ref inputFilePath, ref outputFolderPath))
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
                            var inputFile = new FileInfo(inputFilePath);
                            inputFilePathFull = inputFile.FullName;
                            success = ConvertPHRPDataToXML(inputFilePathFull, outputFolderPath);
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

            return success;
        }

        protected void ShowWarningMessage(string warningMessage)
        {
            ShowMessage("Warning: " + warningMessage);
        }

        private void SetLocalErrorCode(PeptideListToXMLErrorCodes newErrorCode)
        {
            SetLocalErrorCode(newErrorCode, false);
        }

        private void SetLocalErrorCode(PeptideListToXMLErrorCodes newErrorCode, bool leaveExistingErrorCodeUnchanged)
        {
            if (leaveExistingErrorCodeUnchanged && mLocalErrorCode != PeptideListToXMLErrorCodes.NoError)
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

        protected bool WriteCachedData(string inputFilePath, string outputFilePath, PHRPReader.Data.SearchEngineParameters searchEngineParams)
        {
            int spectra;
            int peptides;
            bool success;

            var currentSpectrum = new PepXMLWriter.SpectrumInfoType();

            ResetProgress("Creating the .pepXML file");
            try
            {
                Console.WriteLine();
                ShowMessage("Creating PepXML file at " + Path.GetFileName(outputFilePath));
                mXMLWriter = new PepXMLWriter(mDatasetName, mFastaFilePath, searchEngineParams, inputFilePath, outputFilePath);
                RegisterEvents(mXMLWriter);
                if (!mXMLWriter.IsWritable)
                {
                    ShowErrorMessage("XMLWriter is not writable; aborting");
                    return false;
                }

                mXMLWriter.MaxProteinsPerPSM = mMaxProteinsPerPSM;
                spectra = 0;
                peptides = 0;

                foreach (var spectrumKey in mPSMsBySpectrumKey.Keys)
                {
                    var psm = mPSMsBySpectrumKey[spectrumKey];

                    if (mSpectrumInfo.TryGetValue(spectrumKey, out currentSpectrum))
                    {
                        spectra += 1;
                        peptides += psm.Count;

                        mXMLWriter.WriteSpectrum(ref currentSpectrum, psm, ref mSeqToProteinMapCached);
                    }
                    else
                    {
                        ShowErrorMessage("Spectrum key '" + spectrumKey + "' not found in mSpectrumInfo; this is unexpected");
                    }

                    float pctComplete = spectra / (float)mPSMsBySpectrumKey.Count * 100f;
                    UpdateProgress(pctComplete);
                }

                if (peptides > 500)
                {
                    Console.WriteLine();
                }

                mXMLWriter.CloseDocument();
                Console.WriteLine();
                ShowMessage("PepXML file created with " + spectra.ToString("#,##0") + " spectra and " + peptides.ToString("#,##0") + " peptides");
                success = true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error Reading source file in WriteCachedData: " + ex.Message);
                success = false;
            }

            return success;
        }
    }
}