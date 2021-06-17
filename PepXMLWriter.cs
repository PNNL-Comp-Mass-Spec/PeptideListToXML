using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using PHRPReader;
using PRISM;

namespace PeptideListToXML
{
    public class clsPepXMLWriter : EventNotifier
    {

        // Ignore Spelling: href, stylesheet, xmlns, xsi, xsl, yyyy-MM-ddTHH:mm:ss
        // Ignore Spelling: aminoacid, fval, Inetpub, massd, massdiff, nmc, ntt, peptideprophet, tryptic
        // Ignore Spelling: bscore, deltacn, deltacnstar, hyperscore, msgfspecprob, sprank, spscore, xcorr, yscore

        #region Structures

        public struct udtSpectrumInfoType
        {
            public string SpectrumName;           // Spectrum Title: could be "QC_05_2_05Dec05_Doc_0508-08.9427.9427.1" or just "scan=16134 cs=2"
            public int StartScan;
            public int EndScan;
            public double PrecursorNeutralMass;
            public int AssumedCharge;
            public double ElutionTimeMinutes;
            public string CollisionMode;
            public int Index;
            public string NativeID;
        }

        #endregion

        #region Module-wide variables
        private readonly PeptideMassCalculator mPeptideMassCalculator;
        private XmlWriter mXMLWriter;
        private bool mFileOpen;

        // This dictionary maps PNNL-based score names to pep-xml standard score names
        private Dictionary<string, string> mPNNLScoreNameMap;
        #endregion

        #region Properties

        // ReSharper disable once UnusedMember.Global
        public string DatasetName { get; private set; }

        public bool IsWritable
        {
            get
            {
                return mFileOpen;
            }
        }

        public int MaxProteinsPerPSM { get; set; }

        // ReSharper disable once UnusedMember.Global
        public PHRPReader.Data.SearchEngineParameters SearchEngineParams { get; private set; }

        // ReSharper disable once UnusedMember.Global
        public string SourceFilePath { get; private set; }
        #endregion

        /// <summary>
    /// Instantiate a new PepXML writer
    /// </summary>
    /// <param name="strDatasetName">Name of the Dataset</param>
    /// <param name="strFastaFilePath">Fasta file path to use if objSearchEngineParams.FastaFilePath is empty</param>
    /// <param name="objSearchEngineParams">Search engine parameters</param>
    /// <param name="strSourceFilePath">Source file path</param>
    /// <param name="strOutputFilePath">Path to the PepXML file to create</param>
    /// <remarks></remarks>
        public clsPepXMLWriter(string strDatasetName, string strFastaFilePath, PHRPReader.Data.SearchEngineParameters objSearchEngineParams, string strSourceFilePath, string strOutputFilePath)
        {
            SearchEngineParams = objSearchEngineParams;
            DatasetName = strDatasetName;
            if (DatasetName is null)
                DatasetName = "Unknown";
            if (string.IsNullOrEmpty(strSourceFilePath))
                strSourceFilePath = string.Empty;
            SourceFilePath = strSourceFilePath;
            mPeptideMassCalculator = new PeptideMassCalculator();
            InitializePNNLScoreNameMap();
            MaxProteinsPerPSM = 0;
            try
            {
                if (!InitializePepXMLFile(strOutputFilePath, strFastaFilePath))
                {
                    throw new Exception("Error initializing PepXML file");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error initializing PepXML file: " + ex.Message, ex);
            }
        }

        public bool CloseDocument()
        {
            if (!mFileOpen)
            {
                return false;
            }

            mXMLWriter.WriteEndElement();                // msms_run_summary
            mXMLWriter.WriteEndElement();                // msms_pipeline_analysis
            mXMLWriter.WriteEndDocument();
            mXMLWriter.Flush();
            mXMLWriter.Close();
            return true;
        }

        private bool GetPepXMLCollisionMode(string strPSMCollisionMode, ref string strPepXMLCollisionMode)
        {
            string strCollisionModeUCase = strPSMCollisionMode.ToUpper();
            switch (strCollisionModeUCase ?? "")
            {
                case "CID":
                case "ETD":
                case "HCD":
                    {
                        strPepXMLCollisionMode = strCollisionModeUCase;
                        break;
                    }

                case "ETD/CID":
                case "ETD-CID":
                    {
                        strPepXMLCollisionMode = "ETD/CID";
                        break;
                    }

                default:
                    {
                        if (strCollisionModeUCase.StartsWith("CID"))
                        {
                            strPepXMLCollisionMode = "CID";
                        }
                        else if (strCollisionModeUCase.StartsWith("HCD"))
                        {
                            strPepXMLCollisionMode = "HCD";
                        }
                        else if (strCollisionModeUCase.StartsWith("ETD"))
                        {
                            strPepXMLCollisionMode = "ETD";
                        }
                        else
                        {
                            strPepXMLCollisionMode = string.Empty;
                        }

                        break;
                    }
            }

            if (string.IsNullOrEmpty(strPepXMLCollisionMode))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
    /// Initialize a Pep.XML file for writing
    /// </summary>
    /// <param name="strOutputFilePath"></param>
    /// <param name="strFastaFilePath"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        private bool InitializePepXMLFile(string strOutputFilePath, string strFastaFilePath)
        {
            FileInfo fiOutputFile;
            if (string.IsNullOrWhiteSpace(strFastaFilePath))
            {
                strFastaFilePath = @"C:\Database\Unknown_Database.fasta";
            }

            fiOutputFile = new FileInfo(strOutputFilePath);
            var oSettings = new XmlWriterSettings();
            oSettings.Indent = true;
            oSettings.OmitXmlDeclaration = false;
            oSettings.NewLineOnAttributes = false;
            oSettings.Encoding = Encoding.ASCII;
            mXMLWriter = XmlWriter.Create(strOutputFilePath, oSettings);
            mFileOpen = true;
            mXMLWriter.WriteStartDocument();
            mXMLWriter.WriteProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"pepXML_std.xsl\"");
            WriteHeaderElements(fiOutputFile);
            WriteSearchSummary(strFastaFilePath);
            return true;
        }

        private void InitializePNNLScoreNameMap()
        {
            mPNNLScoreNameMap = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

            // SEQUEST scores
            mPNNLScoreNameMap.Add("XCorr", "xcorr");
            mPNNLScoreNameMap.Add("DelCn", "deltacn");
            mPNNLScoreNameMap.Add("Sp", "spscore");
            mPNNLScoreNameMap.Add("DelCn2", "deltacnstar");
            mPNNLScoreNameMap.Add("RankSp", "sprank");

            // X!Tandem scores
            mPNNLScoreNameMap.Add("Peptide_Hyperscore", "hyperscore");
            mPNNLScoreNameMap.Add("Peptide_Expectation_Value", "expect");
            mPNNLScoreNameMap.Add("y_score", "yscore");
            mPNNLScoreNameMap.Add("b_score", "bscore");
        }

        private void WriteAttribute(string strAttributeName, string Value)
        {
            if (string.IsNullOrEmpty(Value))
            {
                mXMLWriter.WriteAttributeString(strAttributeName, string.Empty);
            }
            else
            {
                mXMLWriter.WriteAttributeString(strAttributeName, Value);
            }
        }

        private void WriteAttribute(string strAttributeName, int Value)
        {
            mXMLWriter.WriteAttributeString(strAttributeName, Value.ToString());
        }

        // ReSharper disable once UnusedMember.Local
        private void WriteAttribute(string strAttributeName, float Value)
        {
            WriteAttribute(strAttributeName, Value, DigitsOfPrecision: 4);
        }

        private void WriteAttribute(string strAttributeName, float Value, int DigitsOfPrecision)
        {
            string strFormatString = "0";
            if (DigitsOfPrecision > 0)
            {
                strFormatString += "." + new string('0', DigitsOfPrecision);
            }

            mXMLWriter.WriteAttributeString(strAttributeName, Value.ToString(strFormatString));
        }

        private void WriteAttributePlusMinus(string strAttributeName, double Value, int DigitsOfPrecision)
        {
            mXMLWriter.WriteAttributeString(strAttributeName, PHRPReader.Reader.SynFileReaderBaseClass.NumToStringPlusMinus(Value, DigitsOfPrecision));
        }

        private void WriteAttribute(string strAttributeName, double Value)
        {
            WriteAttribute(strAttributeName, Value, DigitsOfPrecision: 4);
        }

        private void WriteAttribute(string strAttributeName, double Value, int DigitsOfPrecision)
        {
            string strFormatString = "0";
            if (DigitsOfPrecision > 0)
            {
                strFormatString += "." + new string('0', DigitsOfPrecision);
            }

            mXMLWriter.WriteAttributeString(strAttributeName, Value.ToString(strFormatString));
        }

        private void WriteNameValueElement(string strElementName, string strName, string Value)
        {
            mXMLWriter.WriteStartElement(strElementName);
            WriteAttribute("name", strName);
            WriteAttribute("value", Value);
            mXMLWriter.WriteEndElement();
        }

        private void WriteNameValueElement(string strElementName, string strName, double Value, int DigitsOfPrecision)
        {
            mXMLWriter.WriteStartElement(strElementName);
            WriteAttribute("name", strName);
            WriteAttribute("value", Value, DigitsOfPrecision);
            mXMLWriter.WriteEndElement();
        }

        private void WriteHeaderElements(FileSystemInfo fiOutputFile)
        {
            DateTime dtSearchDate;
            {
                var withBlock = mXMLWriter;
                withBlock.WriteStartElement("msms_pipeline_analysis", "http://regis-web.systemsbiology.net/pepXML");
                withBlock.WriteAttributeString("date", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
                withBlock.WriteAttributeString("summary_xml", fiOutputFile.Name);
                withBlock.WriteAttributeString("xmlns", "http://regis-web.systemsbiology.net/pepXML");
                withBlock.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");

                // Old:               ("xsi", "schemaLocation", Nothing, "http://regis-web.systemsbiology.net/pepXML c:\Inetpub\wwwrootpepXML_v113.xsd")
                withBlock.WriteAttributeString("xsi", "schemaLocation", null, "http://sashimi.sourceforge.net/schema_revision/pepXML/pepXML_v117.xsd");
                withBlock.WriteStartElement("analysis_summary");
                dtSearchDate = SearchEngineParams.SearchDate;
                if (dtSearchDate < new DateTime(1980, 1, 2))
                {
                    // Use the date of the input file since the reported SearchDate is invalid
                    var fiSourceFile = new FileInfo(SourceFilePath);
                    if (fiSourceFile.Exists)
                    {
                        dtSearchDate = fiSourceFile.LastWriteTime;
                    }
                }

                withBlock.WriteAttributeString("time", dtSearchDate.ToString("yyyy-MM-ddTHH:mm:ss"));
                withBlock.WriteAttributeString("analysis", SearchEngineParams.SearchEngineName);
                withBlock.WriteAttributeString("version", SearchEngineParams.SearchEngineVersion);
                withBlock.WriteEndElement();
                withBlock.WriteStartElement("msms_run_summary");
                withBlock.WriteAttributeString("base_name", DatasetName);
                withBlock.WriteAttributeString("raw_data_type", "raw");
                withBlock.WriteAttributeString("raw_data", ".mzXML");
                withBlock.WriteStartElement("sample_enzyme");
                withBlock.WriteAttributeString("name", SearchEngineParams.Enzyme);

                // ToDo: get the specificity info from mSearchEngineParams

                if (SearchEngineParams.Enzyme.ToLower().Contains("trypsin"))
                {
                    withBlock.WriteStartElement("specificity");
                    withBlock.WriteAttributeString("cut", "KR");
                    withBlock.WriteAttributeString("no_cut", "P");
                    withBlock.WriteAttributeString("sense", "C");
                    withBlock.WriteEndElement();
                }
                else
                {
                    withBlock.WriteStartElement("specificity");
                    withBlock.WriteAttributeString("cut", "KR");
                    withBlock.WriteAttributeString("no_cut", "P");
                    withBlock.WriteAttributeString("sense", "C");
                    withBlock.WriteEndElement();
                }

                withBlock.WriteEndElement();                  // sample_enzyme
            }
        }

        private void WriteSearchSummary(string strFastaFilePath)
        {
            SortedSet<char> lstTerminalSymbols;
            lstTerminalSymbols = PHRPReader.Data.ModificationDefinition.GetTerminalSymbols();
            string strFastaFilePathToUse;
            string strTargetResidues;
            double dblAAMass;
            {
                var withBlock = mXMLWriter;
                withBlock.WriteStartElement("search_summary");
                withBlock.WriteAttributeString("base_name", DatasetName);
                withBlock.WriteAttributeString("source_file", Path.GetFileName(SourceFilePath));
                withBlock.WriteAttributeString("search_engine", SearchEngineParams.SearchEngineName);
                withBlock.WriteAttributeString("search_engine_version", SearchEngineParams.SearchEngineVersion);
                withBlock.WriteAttributeString("precursor_mass_type", SearchEngineParams.PrecursorMassType);
                withBlock.WriteAttributeString("fragment_mass_type", SearchEngineParams.FragmentMassType);
                withBlock.WriteAttributeString("search_id", "1");
                withBlock.WriteStartElement("search_database");
                if (!string.IsNullOrEmpty(SearchEngineParams.FastaFilePath))
                {
                    try
                    {
                        // Update the folder to be the start with C:\Database
                        strFastaFilePathToUse = Path.Combine(@"C:\Database", Path.GetFileName(SearchEngineParams.FastaFilePath));
                    }
                    catch (Exception ex)
                    {
                        strFastaFilePathToUse = SearchEngineParams.FastaFilePath;
                    }
                }
                else
                {
                    strFastaFilePathToUse = string.Copy(strFastaFilePath);
                }

                withBlock.WriteAttributeString("local_path", strFastaFilePathToUse);
                withBlock.WriteAttributeString("type", "AA");
                withBlock.WriteEndElement();      // search_database
            }

            mXMLWriter.WriteStartElement("enzymatic_search_constraint");
            WriteAttribute("enzyme", SearchEngineParams.Enzyme);
            WriteAttribute("max_num_internal_cleavages", SearchEngineParams.MaxNumberInternalCleavages);
            WriteAttribute("min_number_termini", SearchEngineParams.MinNumberTermini);
            mXMLWriter.WriteEndElement();        // enzymatic_search_constraint

            // Amino acid mod details
            foreach (PHRPReader.Data.ModificationDefinition objModDef in SearchEngineParams.ModList)
            {
                if (objModDef.CanAffectPeptideResidues())
                {
                    if (string.IsNullOrEmpty(objModDef.TargetResidues))
                    {
                        // This modification can affect any amino acid (skip BJOUXZ)
                        strTargetResidues = "ACDEFGHIKLMNPQRSTVWY";
                    }
                    else
                    {
                        strTargetResidues = objModDef.TargetResidues;
                    }

                    foreach (var chChar in strTargetResidues)
                    {
                        if (!lstTerminalSymbols.Contains(chChar))
                        {
                            mXMLWriter.WriteStartElement("aminoacid_modification");
                            WriteAttribute("aminoacid", chChar.ToString());                         // Amino acid symbol, e.g. A
                            WriteAttributePlusMinus("massdiff", objModDef.ModificationMass, 5);          // Mass difference, must begin with + or -
                            dblAAMass = mPeptideMassCalculator.GetAminoAcidMass(chChar);
                            WriteAttribute("mass", dblAAMass + objModDef.ModificationMass, 4);
                            if (objModDef.ModificationType == PHRPReader.Data.ModificationDefinition.ResidueModificationType.DynamicMod)
                            {
                                WriteAttribute("variable", "Y");
                            }
                            else
                            {
                                WriteAttribute("variable", "N");
                            }

                            WriteAttribute("symbol", objModDef.ModificationSymbol.ToString());              // Symbol used by search-engine to denote this mod
                            WriteAttribute("description", objModDef.MassCorrectionTag);
                            mXMLWriter.WriteEndElement();        // aminoacid_modification
                        }
                    }
                }
            }

            // Protein/Peptide terminal mods
            foreach (PHRPReader.Data.ModificationDefinition objModDef in SearchEngineParams.ModList)
            {
                if (objModDef.CanAffectPeptideOrProteinTerminus())
                {
                    if (string.IsNullOrEmpty(objModDef.TargetResidues))
                    {
                        // Target residues should not be empty for terminal mods
                        // But, we'll list them anyway
                        strTargetResidues = string.Format("{0}{1}",
                            PHRPReader.Data.AminoAcidModInfo.N_TERMINAL_PEPTIDE_SYMBOL_DMS,
                            PHRPReader.Data.AminoAcidModInfo.C_TERMINAL_PEPTIDE_SYMBOL_DMS);
                    }
                    else
                    {
                        strTargetResidues = objModDef.TargetResidues;
                    }

                    foreach (var chChar in strTargetResidues)
                    {
                        if (lstTerminalSymbols.Contains(chChar))
                        {
                            mXMLWriter.WriteStartElement("terminal_modification");
                            if (chChar == PHRPReader.Data.AminoAcidModInfo.C_TERMINAL_PEPTIDE_SYMBOL_DMS || chChar == PHRPReader.Data.AminoAcidModInfo.C_TERMINAL_PROTEIN_SYMBOL_DMS)
                            {
                                WriteAttribute("terminus", "c");
                                dblAAMass = PeptideMassCalculator.DEFAULT_C_TERMINUS_MASS_CHANGE;
                            }
                            else
                            {
                                WriteAttribute("terminus", "n");
                                dblAAMass = PeptideMassCalculator.DEFAULT_N_TERMINUS_MASS_CHANGE;
                            }

                            WriteAttributePlusMinus("massdiff", objModDef.ModificationMass, 5);          // Mass difference, must begin with + or -
                            WriteAttribute("mass", dblAAMass + objModDef.ModificationMass, 4);
                            if (objModDef.ModificationType == PHRPReader.Data.ModificationDefinition.ResidueModificationType.DynamicMod)
                            {
                                WriteAttribute("variable", "Y");
                            }
                            else
                            {
                                WriteAttribute("variable", "N");
                            }

                            WriteAttribute("symbol", objModDef.ModificationSymbol.ToString());              // Symbol used by search-engine to denote this mod
                            if (chChar == PHRPReader.Data.AminoAcidModInfo.N_TERMINAL_PROTEIN_SYMBOL_DMS || chChar == PHRPReader.Data.AminoAcidModInfo.C_TERMINAL_PROTEIN_SYMBOL_DMS)
                            {
                                // Modification can only occur at the protein terminus
                                WriteAttribute("protein_terminus", "Y");
                            }
                            else
                            {
                                WriteAttribute("protein_terminus", "N");
                            }

                            WriteAttribute("description", objModDef.MassCorrectionTag);
                            mXMLWriter.WriteEndElement();        // terminal_modification
                        }
                    }
                }
            }

            // Parameters specific to the search engine
            if (SearchEngineParams.Parameters is null || SearchEngineParams.Parameters.Count == 0)
            {
                // Write out two dummy-parameters
                mXMLWriter.WriteComment("Dummy search-engine parameters");
                WriteNameValueElement("parameter", "peptide_mass_tol", "3.000");
                WriteNameValueElement("parameter", "fragment_ion_tol", "0.000");
            }
            else
            {
                mXMLWriter.WriteComment("Search-engine parameters");

                // Write out the search-engine parameters
                foreach (KeyValuePair<string, string> objItem in SearchEngineParams.Parameters)
                    WriteNameValueElement("parameter", objItem.Key, objItem.Value);
            }

            mXMLWriter.WriteEndElement();                    // search_summary
        }

        public void WriteSpectrum(ref udtSpectrumInfoType objSpectrum, List<PHRPReader.Data.PSM> lstHits, ref SortedList<int, List<PHRPReader.Data.ProteinInfo>> lstSeqToProteinMap)
        {
            double dblMassErrorDa;
            double dblMassErrorPPM;
            double dblTotalMass;
            string strAlternateScoreName = string.Empty;
            string strCollisionMode = string.Empty;

            // The keys in this dictionary are the residue position in the peptide; the values are the total mass (including all mods)
            Dictionary<int, double> lstModifiedResidues;
            lstModifiedResidues = new Dictionary<int, double>();
            if (lstHits is null || lstHits.Count == 0)
                return;
            {
                var withBlock = mXMLWriter;
                withBlock.WriteStartElement("spectrum_query");
                withBlock.WriteAttributeString("spectrum", objSpectrum.SpectrumName);         // Example: QC_05_2_05Dec05_Doc_0508-08.9427.9427.1
                WriteAttribute("start_scan", objSpectrum.StartScan);
                WriteAttribute("end_scan", objSpectrum.EndScan);
                WriteAttribute("retention_time_sec", objSpectrum.ElutionTimeMinutes * 60.0d, 2);
                if (GetPepXMLCollisionMode(objSpectrum.CollisionMode, ref strCollisionMode))
                {
                    WriteAttribute("activation_method", strCollisionMode);
                }

                WriteAttribute("precursor_neutral_mass", objSpectrum.PrecursorNeutralMass);
                WriteAttribute("assumed_charge", objSpectrum.AssumedCharge);
                WriteAttribute("index", objSpectrum.Index);
                WriteAttribute("spectrumNativeID", objSpectrum.NativeID);            // Example: controllerType=0 controllerNumber=1 scan=20554
                withBlock.WriteStartElement("search_result");
            }

            foreach (PHRPReader.Data.PSM oPSMEntry in lstHits)
            {
                mXMLWriter.WriteStartElement("search_hit");
                WriteAttribute("hit_rank", oPSMEntry.ScoreRank);
                string strPeptide = string.Empty;
                string strCleanSequence;
                string strPrefix = string.Empty;
                string strSuffix = string.Empty;
                if (PeptideCleavageStateCalculator.SplitPrefixAndSuffixFromSequence(oPSMEntry.Peptide, out strPeptide, out strPrefix, out strSuffix))
                {
                    // The peptide sequence needs to be just the amino acids; no mod symbols
                    strCleanSequence = PeptideCleavageStateCalculator.ExtractCleanSequenceFromSequenceWithMods(strPeptide, false);
                    mXMLWriter.WriteAttributeString("peptide", strCleanSequence);
                    mXMLWriter.WriteAttributeString("peptide_prev_aa", strPrefix);
                    mXMLWriter.WriteAttributeString("peptide_next_aa", strSuffix);
                }
                else
                {
                    strCleanSequence = PeptideCleavageStateCalculator.ExtractCleanSequenceFromSequenceWithMods(oPSMEntry.Peptide, false);
                    mXMLWriter.WriteAttributeString("peptide", strCleanSequence);
                    mXMLWriter.WriteAttributeString("peptide_prev_aa", string.Empty);
                    mXMLWriter.WriteAttributeString("peptide_next_aa", string.Empty);
                }

                if ((strCleanSequence ?? "") != (strPeptide ?? ""))
                {
                    mXMLWriter.WriteAttributeString("peptide_with_mods", oPSMEntry.PeptideWithNumericMods);
                }

                mXMLWriter.WriteAttributeString("protein", oPSMEntry.ProteinFirst);

                // Could optionally write out protein description
                // .WriteAttributeString("protein_descr", objSearchHit.StrProteinDescription)

                WriteAttribute("num_tot_proteins", oPSMEntry.Proteins.Count);
                WriteAttribute("num_matched_ions", 0);
                WriteAttribute("tot_num_ions", 0);
                WriteAttribute("calc_neutral_pep_mass", oPSMEntry.PeptideMonoisotopicMass, 4);
                if (!double.TryParse(oPSMEntry.MassErrorDa, out dblMassErrorDa))
                {
                    dblMassErrorDa = 0d;
                }

                WriteAttributePlusMinus("massdiff", dblMassErrorDa, 5);

                // Write the number of tryptic ends (0 for non-tryptic, 1 for partially tryptic, 2 for fully tryptic)
                WriteAttribute("num_tol_term", oPSMEntry.NumTrypticTermini);
                WriteAttribute("num_missed_cleavages", oPSMEntry.NumMissedCleavages);

                // Initially all peptides will have "is_rejected" = 0
                WriteAttribute("is_rejected", 0);
                List<PHRPReader.Data.ProteinInfo> lstProteins = null;
                bool blnProteinInfoAvailable;
                int intNumTrypticTermini;
                if (lstSeqToProteinMap is object && lstSeqToProteinMap.Count > 0)
                {
                    blnProteinInfoAvailable = lstSeqToProteinMap.TryGetValue(oPSMEntry.SeqID, out lstProteins);
                }
                else
                {
                    blnProteinInfoAvailable = false;
                }

                int intProteinsWritten = 0;

                // Write out the additional proteins
                foreach (string strProteinAddnl in oPSMEntry.Proteins)
                {
                    if ((strProteinAddnl ?? "") != (oPSMEntry.ProteinFirst ?? ""))
                    {
                        mXMLWriter.WriteStartElement("alternative_protein");
                        mXMLWriter.WriteAttributeString("protein", strProteinAddnl);
                        // .WriteAttributeString("protein_descr", strProteinAddnlDescription)

                        // Initially use .NumTrypticTermini
                        // We'll update this using lstProteins if possible
                        intNumTrypticTermini = oPSMEntry.NumTrypticTermini;
                        if (blnProteinInfoAvailable)
                        {
                            foreach (var objProtein in lstProteins)
                            {
                                if ((objProtein.ProteinName ?? "") == (strProteinAddnl ?? ""))
                                {
                                    intNumTrypticTermini = (int)objProtein.CleavageState;
                                    break;
                                }
                            }
                        }

                        WriteAttribute("num_tol_term", intNumTrypticTermini);
                        mXMLWriter.WriteEndElement();      // alternative_protein
                    }

                    intProteinsWritten += 1;
                    if (MaxProteinsPerPSM > 0 && intProteinsWritten >= MaxProteinsPerPSM)
                    {
                        break;
                    }
                }

                if (oPSMEntry.ModifiedResidues.Count > 0)
                {
                    mXMLWriter.WriteStartElement("modification_info");
                    double dblNTermAddon = 0d;
                    double dblCTermAddon = 0d;

                    // Look for N and C terminal mods in oPSMEntry.ModifiedResidues
                    foreach (var objResidue in oPSMEntry.ModifiedResidues)
                    {
                        if (objResidue.ModDefinition.ModificationType == PHRPReader.Data.ModificationDefinition.ResidueModificationType.TerminalPeptideStaticMod || objResidue.ModDefinition.ModificationType == PHRPReader.Data.ModificationDefinition.ResidueModificationType.ProteinTerminusStaticMod)
                        {
                            switch (objResidue.TerminusState)
                            {
                                case PHRPReader.Data.AminoAcidModInfo.ResidueTerminusState.PeptideNTerminus:
                                case PHRPReader.Data.AminoAcidModInfo.ResidueTerminusState.ProteinNTerminus:
                                case PHRPReader.Data.AminoAcidModInfo.ResidueTerminusState.ProteinNandCCTerminus:
                                    {
                                        dblNTermAddon += objResidue.ModDefinition.ModificationMass;
                                        break;
                                    }

                                case PHRPReader.Data.AminoAcidModInfo.ResidueTerminusState.PeptideCTerminus:
                                case PHRPReader.Data.AminoAcidModInfo.ResidueTerminusState.ProteinCTerminus:
                                    {
                                        dblCTermAddon += objResidue.ModDefinition.ModificationMass;
                                        break;
                                    }

                                default:
                                    {
                                        // This is unexpected
                                        OnErrorEvent("Peptide or Protein terminal mod found, but residue is not at a peptide or protein terminus: " + objResidue.Residue + " at position " + objResidue.ResidueLocInPeptide + " in peptide " + oPSMEntry.Peptide + ", scan " + oPSMEntry.ScanNumber);
                                        break;
                                    }
                            }
                        }
                    }

                    // If a peptide-terminal mod, add either of these attributes:
                    if (Math.Abs(dblNTermAddon) > float.Epsilon)
                    {
                        WriteAttributePlusMinus("mod_nterm_mass", PeptideMassCalculator.DEFAULT_N_TERMINUS_MASS_CHANGE + dblNTermAddon, 5);
                    }

                    if (Math.Abs(dblCTermAddon) > float.Epsilon)
                    {
                        WriteAttributePlusMinus("mod_cterm_mass", PeptideMassCalculator.DEFAULT_C_TERMINUS_MASS_CHANGE + dblCTermAddon, 5);
                    }


                    // Write out an entry for each modified amino acid
                    // We need to keep track of the total mass of each modified residue (excluding terminal mods) since a residue could have multiple modifications
                    lstModifiedResidues.Clear();
                    foreach (var objResidue in oPSMEntry.ModifiedResidues)
                    {
                        if (!(objResidue.ModDefinition.ModificationType == PHRPReader.Data.ModificationDefinition.ResidueModificationType.TerminalPeptideStaticMod || objResidue.ModDefinition.ModificationType == PHRPReader.Data.ModificationDefinition.ResidueModificationType.ProteinTerminusStaticMod))
                        {
                            if (lstModifiedResidues.TryGetValue(objResidue.ResidueLocInPeptide, out dblTotalMass))
                            {
                                // This residue has more than one modification applied to it
                                dblTotalMass += objResidue.ModDefinition.ModificationMass;
                                lstModifiedResidues[objResidue.ResidueLocInPeptide] = dblTotalMass;
                            }
                            else
                            {
                                dblTotalMass = mPeptideMassCalculator.GetAminoAcidMass(objResidue.Residue) + objResidue.ModDefinition.ModificationMass;
                                lstModifiedResidues.Add(objResidue.ResidueLocInPeptide, dblTotalMass);
                            }
                        }
                    }

                    foreach (var objItem in lstModifiedResidues)
                    {
                        mXMLWriter.WriteStartElement("mod_aminoacid_mass");
                        WriteAttribute("position", objItem.Key);     // Position of residue in peptide
                        WriteAttribute("mass", objItem.Value, 5);    // Total amino acid mass, including all mods (but excluding N or C terminal mods)
                        mXMLWriter.WriteEndElement();      // mod_aminoacid_mass
                    }

                    mXMLWriter.WriteEndElement();      // modification_info
                }

                // Write out the search scores
                foreach (var objItem in oPSMEntry.AdditionalScores)
                {
                    if (mPNNLScoreNameMap.TryGetValue(objItem.Key, out strAlternateScoreName))
                    {
                        WriteNameValueElement("search_score", strAlternateScoreName, objItem.Value);
                    }
                    else
                    {
                        WriteNameValueElement("search_score", objItem.Key, objItem.Value);
                    }
                }

                WriteNameValueElement("search_score", "msgfspecprob", oPSMEntry.MSGFSpecEValue);

                // Write out the mass error ppm value as a custom search score
                WriteNameValueElement("search_score", "MassErrorPPM", oPSMEntry.MassErrorPPM);
                if (!double.TryParse(oPSMEntry.MassErrorPPM, out dblMassErrorPPM))
                {
                    dblMassErrorPPM = 0d;
                }

                WriteNameValueElement("search_score", "AbsMassErrorPPM", Math.Abs(dblMassErrorPPM), 4);

                // ' Old, unused
                // WritePeptideProphetUsingMSGF(mXMLWriter, objSearchHit, iNumTrypticTermini, iNumMissedCleavages)

                mXMLWriter.WriteEndElement();              // search_hit
            }

            mXMLWriter.WriteEndElement();            // search_result
            mXMLWriter.WriteEndElement();            // spectrum_query
        }

        // Old, unused
        // Private Sub WritePeptideProphetUsingMSGF(ByRef mXMLWriter As System.Xml.XmlWriter, ByRef objSearchHit As clsSearchHit, ByVal iNumTrypticTermini As Integer, ByVal iNumMissedCleavages As Integer)

        // Dim strMSGF As String
        // Dim strFVal As String

        // With mXMLWriter
        // .WriteStartElement("analysis_result")
        // .WriteAttributeString("analysis", "peptideprophet")


        // .WriteStartElement("peptideprophet_result")
        // strMSGF = clsMSGFConversion.MSGFToProbability(objSearchHit.dMSGFSpecProb).ToString("0.0000")
        // strFVal = clsMSGFConversion.MSGFToFValue(objSearchHit.dMSGFSpecProb).ToString("0.0000")

        // .WriteAttributeString("probability", strMSGF)
        // .WriteAttributeString("all_ntt_prob", "(" & strMSGF & "," & strMSGF & "," & strMSGF & ")")

        // .WriteStartElement("search_score_summary")

        // .WriteStartElement("parameter")
        // .WriteAttributeString("name", "fval")
        // .WriteAttributeString("value", strFVal)
        // .WriteEndElement()

        // .WriteStartElement("parameter")
        // .WriteAttributeString("name", "ntt")
        // .WriteAttributeString("value", iNumTrypticTermini.ToString("0"))
        // .WriteEndElement()

        // .WriteStartElement("parameter")
        // .WriteAttributeString("name", "nmc")
        // .WriteAttributeString("value", iNumMissedCleavages.ToString("0"))
        // .WriteEndElement()

        // .WriteStartElement("parameter")
        // .WriteAttributeString("name", "massd")
        // .WriteAttributeString("value", objSearchHit.dMassdiff.ToString("0.000"))
        // .WriteEndElement()

        // .WriteEndElement()			  ' search_score_summary

        // .WriteEndElement()			  ' peptideprophet_result

        // .WriteEndElement()			  ' analysis_result

        // End With

        // End Sub

        // Old, unused
        // Private Class clsMSGFConversion

        // ''' <summary>
        // ''' Performs a crude approximation of Probability using a MSGF SpecProb value
        // ''' Converts the MSGF score to base-10 log, adds 6, then converts back to the original scale to obtain an adjusted MSGF SpecProb value
        // ''' For example, if MSGF SpecProb = 1E-13, then the adjusted value is 1E-07
        // ''' Computes Probability as 1 - AdjustedMSGFSpecProb
        // ''' </summary>
        // ''' <param name="dblMSGFScore">MSGF SpecProb to convert</param>
        // ''' <returns>Probability</returns>
        // Public Shared Function MSGFToProbability(dblMSGFScore As Double) As Double
        // Constant LOG_MSGF_ADJUST As Integer = 6

        // Dim dLogMSGF As Double
        // Dim dblProbability As Double

        // If dblMSGFScore >= 1 Then
        // dblProbability = 0
        // ElseIf dblMSGFScore <= 0 Then
        // dblProbability = 1
        // Else
        // dLogMSGF = Math.Log(dblMSGFScore, 10) + LOG_MSGF_ADJUST
        // dblProbability = 1 - Math.Pow(10, dLogMSGF)

        // If dblProbability < 0 Then
        // dblProbability = 0
        // End If

        // If dblProbability > 1 Then
        // dblProbability = 1
        // End If
        // End If

        // Return dblProbability

        // End Function

        // ''' <summary>
        // ''' Performs a crude approximation of FValue using a MSGF SpecProb value
        // ''' Converts the MSGF score to base-10 log, adds 6, then takes the negative of this result
        // ''' For example, if MSGF SpecProb = 1E-13, then computes: -13 + 6 = -7, then returns 7
        // ''' </summary>
        // ''' <param name="dblMSGFScore">MSGF SpecProb to convert</param>
        // ''' <returns>FValue</returns>
        // Public Shared Function MSGFToFValue(dblMSGFScore As Double) As Double
        // Constant LOG_MSGF_ADJUST As Integer = 6

        // Dim dLogMSGF As Double
        // Dim dblFValue As Double

        // If dblMSGFScore >= 1 Then
        // dblFValue = 0
        // Else
        // dLogMSGF = Math.Log(dblMSGFScore, 10) + LOG_MSGF_ADJUST
        // dblFValue = -dLogMSGF
        // End If

        // Return dblFValue

        // End Function

        // End Class
    }
}