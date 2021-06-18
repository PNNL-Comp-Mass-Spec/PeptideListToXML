using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using PHRPReader;
using PRISM;

namespace PeptideListToXML
{
    /// <summary>
    /// PepXML writer
    /// </summary>
    public class PepXMLWriter : EventNotifier
    {
        // Ignore Spelling: href, stylesheet, xmlns, xsi, xsl, yyyy-MM-ddTHH:mm:ss
        // Ignore Spelling: aminoacid, fval, Inetpub, massd, massdiff, nmc, ntt, peptideprophet, tryptic
        // Ignore Spelling: bscore, deltacn, deltacnstar, hyperscore, msgfspecprob, sprank, spscore, xcorr, yscore

        #region Structures

        /// <summary>
        /// Spectrum Info
        /// </summary>
        public struct SpectrumInfoType
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

        // This dictionary maps PNNL-based score names to pep-xml standard score names
        private Dictionary<string, string> mPNNLScoreNameMap;
        #endregion

        #region Properties

        /// <summary>
        /// Dataset name
        /// </summary>
        public string DatasetName { get; }

        /// <summary>
        /// Maximum number of proteins per PSM to store
        /// </summary>
        public int MaxProteinsPerPSM { get; set; }

        /// <summary>
        /// Search engine parameters, read by PHRPReader
        /// </summary>
        public PHRPReader.Data.SearchEngineParameters SearchEngineParams { get; }

        public string SourceFilePath { get; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="datasetName">Name of the Dataset</param>
        /// <param name="fastaFilePath">Fasta file path to use if searchEngineParams.FastaFilePath is empty</param>
        /// <param name="searchEngineParams">Search engine parameters</param>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="outputFilePath">Path to the PepXML file to create</param>
        /// <remarks></remarks>
        public PepXMLWriter(string datasetName, string fastaFilePath, PHRPReader.Data.SearchEngineParameters searchEngineParams, string sourceFilePath, string outputFilePath)
        {
            SearchEngineParams = searchEngineParams;

            DatasetName = datasetName ?? "Unknown";

            if (string.IsNullOrEmpty(sourceFilePath))
                sourceFilePath = string.Empty;
            SourceFilePath = sourceFilePath;
            mPeptideMassCalculator = new PeptideMassCalculator();
            InitializePNNLScoreNameMap();
            MaxProteinsPerPSM = 0;
            try
            {
                if (!InitializePepXMLFile(outputFilePath, fastaFilePath))
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
            mXMLWriter.WriteEndElement();                // msms_run_summary
            mXMLWriter.WriteEndElement();                // msms_pipeline_analysis
            mXMLWriter.WriteEndDocument();
            mXMLWriter.Flush();
            mXMLWriter.Close();
            return true;
        }

        private bool GetPepXMLCollisionMode(string psmCollisionMode, ref string pepXMLCollisionMode)
        {
            string collisionModeUCase = psmCollisionMode.ToUpper();
            switch (collisionModeUCase ?? string.Empty)
            {
                case "CID":
                case "ETD":
                case "HCD":
                    {
                        pepXMLCollisionMode = collisionModeUCase;
                        break;
                    }

                case "ETD/CID":
                case "ETD-CID":
                    {
                        pepXMLCollisionMode = "ETD/CID";
                        break;
                    }

                default:
                    {
                        if (collisionModeUCase.StartsWith("CID"))
                        {
                            pepXMLCollisionMode = "CID";
                        }
                        else if (collisionModeUCase.StartsWith("HCD"))
                        {
                            pepXMLCollisionMode = "HCD";
                        }
                        else if (collisionModeUCase.StartsWith("ETD"))
                        {
                            pepXMLCollisionMode = "ETD";
                        }
                        else
                        {
                            pepXMLCollisionMode = string.Empty;
                        }

                        break;
                    }
            }

            if (string.IsNullOrEmpty(pepXMLCollisionMode))
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
        /// <param name="outputFilePath"></param>
        /// <param name="fastaFilePath"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        private bool InitializePepXMLFile(string outputFilePath, string fastaFilePath)
        {
            FileInfo outputFile;
            if (string.IsNullOrWhiteSpace(fastaFilePath))
            {
                fastaFilePath = @"C:\Database\Unknown_Database.fasta";
            }

            outputFile = new FileInfo(outputFilePath);
            var writerSettings = new XmlWriterSettings();
            writerSettings.Indent = true;
            writerSettings.OmitXmlDeclaration = false;
            writerSettings.NewLineOnAttributes = false;
            writerSettings.Encoding = Encoding.ASCII;

            mXMLWriter = XmlWriter.Create(outputFilePath, writerSettings);

            mXMLWriter.WriteStartDocument();
            mXMLWriter.WriteProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"pepXML_std.xsl\"");
            WriteHeaderElements(outputFile);
            WriteSearchSummary(fastaFilePath);
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

        private void WriteAttribute(string attributeName, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                mXMLWriter.WriteAttributeString(attributeName, string.Empty);
            }
            else
            {
                mXMLWriter.WriteAttributeString(attributeName, value);
            }
        }

        private void WriteAttribute(string attributeName, int value)
        {
            mXMLWriter.WriteAttributeString(attributeName, value.ToString());
        }

        // ReSharper disable once UnusedMember.Local
        private void WriteAttribute(string attributeName, float value)
        {
            WriteAttribute(attributeName, value, digitsOfPrecision: 4);
        }

        private void WriteAttribute(string attributeName, float value, int digitsOfPrecision)
        {
            var formatString = "0";
            if (digitsOfPrecision > 0)
            {
                formatString += "." + new string('0', digitsOfPrecision);
            }

            mXMLWriter.WriteAttributeString(attributeName, value.ToString(formatString));
        }

        private void WriteAttributePlusMinus(string attributeName, double value, int digitsOfPrecision)
        {
            mXMLWriter.WriteAttributeString(attributeName, PHRPReader.Reader.SynFileReaderBaseClass.NumToStringPlusMinus(value, digitsOfPrecision));
        }

        private void WriteAttribute(string attributeName, double value)
        {
            WriteAttribute(attributeName, value, digitsOfPrecision: 4);
        }

        private void WriteAttribute(string attributeName, double value, int digitsOfPrecision)
        {
            var formatString = "0";
            if (digitsOfPrecision > 0)
            {
                formatString += "." + new string('0', digitsOfPrecision);
            }

            mXMLWriter.WriteAttributeString(attributeName, value.ToString(formatString));
        }

        private void WriteNameValueElement(string elementName, string name, string value)
        {
            mXMLWriter.WriteStartElement(elementName);
            WriteAttribute("name", name);
            WriteAttribute("value", value);
            mXMLWriter.WriteEndElement();
        }

        private void WriteNameValueElement(string elementName, string name, double value, int digitsOfPrecision)
        {
            mXMLWriter.WriteStartElement(elementName);
            WriteAttribute("name", name);
            WriteAttribute("value", value, digitsOfPrecision);
            mXMLWriter.WriteEndElement();
        }

        private void WriteHeaderElements(FileSystemInfo outputFile)
        {
            {
                var withBlock = mXMLWriter;
                withBlock.WriteStartElement("msms_pipeline_analysis", "http://regis-web.systemsbiology.net/pepXML");
                withBlock.WriteAttributeString("date", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
                withBlock.WriteAttributeString("summary_xml", outputFile.Name);
                withBlock.WriteAttributeString("xmlns", "http://regis-web.systemsbiology.net/pepXML");
                withBlock.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");

                // Old:               ("xsi", "schemaLocation", Nothing, "http://regis-web.systemsbiology.net/pepXML c:\Inetpub\wwwrootpepXML_v113.xsd")
                withBlock.WriteAttributeString("xsi", "schemaLocation", null, "http://sashimi.sourceforge.net/schema_revision/pepXML/pepXML_v117.xsd");
                withBlock.WriteStartElement("analysis_summary");
                var searchDate = SearchEngineParams.SearchDate;
                if (searchDate < new DateTime(1980, 1, 2))
                {
                    // Use the date of the input file since the reported SearchDate is invalid
                    var sourceFile = new FileInfo(SourceFilePath);
                    if (sourceFile.Exists)
                    {
                        searchDate = sourceFile.LastWriteTime;
                    }
                }

                withBlock.WriteAttributeString("time", searchDate.ToString("yyyy-MM-ddTHH:mm:ss"));
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

        private void WriteSearchSummary(string fastaFilePath)
        {
            SortedSet<char> terminalSymbols;
            terminalSymbols = PHRPReader.Data.ModificationDefinition.GetTerminalSymbols();
            string fastaFilePathToUse;
            string targetResidues;
            double aminoAcidMass;

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
                        fastaFilePathToUse = Path.Combine(@"C:\Database", Path.GetFileName(SearchEngineParams.FastaFilePath));
                    }
                    catch (Exception ex)
                    {
                        fastaFilePathToUse = SearchEngineParams.FastaFilePath;
                    }
                }
                else
                {
                    fastaFilePathToUse = string.Copy(fastaFilePath);
                }

                withBlock.WriteAttributeString("local_path", fastaFilePathToUse);
                withBlock.WriteAttributeString("type", "AA");
                withBlock.WriteEndElement();      // search_database
            }

            mXMLWriter.WriteStartElement("enzymatic_search_constraint");
            WriteAttribute("enzyme", SearchEngineParams.Enzyme);
            WriteAttribute("max_num_internal_cleavages", SearchEngineParams.MaxNumberInternalCleavages);
            WriteAttribute("min_number_termini", SearchEngineParams.MinNumberTermini);
            mXMLWriter.WriteEndElement();        // enzymatic_search_constraint

            // Amino acid mod details
            foreach (PHRPReader.Data.ModificationDefinition modDef in SearchEngineParams.ModList)
            {
                if (modDef.CanAffectPeptideResidues())
                {
                    if (string.IsNullOrEmpty(modDef.TargetResidues))
                    {
                        // This modification can affect any amino acid (skip BJOUXZ)
                        targetResidues = "ACDEFGHIKLMNPQRSTVWY";
                    }
                    else
                    {
                        targetResidues = modDef.TargetResidues;
                    }

                    foreach (var residue in targetResidues)
                    {
                        if (!terminalSymbols.Contains(residue))
                        {
                            mXMLWriter.WriteStartElement("aminoacid_modification");
                            WriteAttribute("aminoacid", residue.ToString());                         // Amino acid symbol, e.g. A
                            WriteAttributePlusMinus("massdiff", modDef.ModificationMass, 5);          // Mass difference, must begin with + or -
                            aminoAcidMass = mPeptideMassCalculator.GetAminoAcidMass(residue);
                            WriteAttribute("mass", aminoAcidMass + modDef.ModificationMass, 4);
                            if (modDef.ModificationType == PHRPReader.Data.ModificationDefinition.ResidueModificationType.DynamicMod)
                            {
                                WriteAttribute("variable", "Y");
                            }
                            else
                            {
                                WriteAttribute("variable", "N");
                            }

                            WriteAttribute("symbol", modDef.ModificationSymbol.ToString());              // Symbol used by search-engine to denote this mod
                            WriteAttribute("description", modDef.MassCorrectionTag);
                            mXMLWriter.WriteEndElement();        // aminoacid_modification
                        }
                    }
                }
            }

            // Protein/Peptide terminal mods
            foreach (PHRPReader.Data.ModificationDefinition modDef in SearchEngineParams.ModList)
            {
                if (modDef.CanAffectPeptideOrProteinTerminus())
                {
                    if (string.IsNullOrEmpty(modDef.TargetResidues))
                    {
                        // Target residues should not be empty for terminal mods
                        // But, we'll list them anyway
                        targetResidues = string.Format("{0}{1}",
                            PHRPReader.Data.AminoAcidModInfo.N_TERMINAL_PEPTIDE_SYMBOL_DMS,
                            PHRPReader.Data.AminoAcidModInfo.C_TERMINAL_PEPTIDE_SYMBOL_DMS);
                    }
                    else
                    {
                        targetResidues = modDef.TargetResidues;
                    }

                    foreach (var residue in targetResidues)
                    {
                        if (terminalSymbols.Contains(residue))
                        {
                            mXMLWriter.WriteStartElement("terminal_modification");
                            if (residue == PHRPReader.Data.AminoAcidModInfo.C_TERMINAL_PEPTIDE_SYMBOL_DMS || residue == PHRPReader.Data.AminoAcidModInfo.C_TERMINAL_PROTEIN_SYMBOL_DMS)
                            {
                                WriteAttribute("terminus", "c");
                                aminoAcidMass = PeptideMassCalculator.DEFAULT_C_TERMINUS_MASS_CHANGE;
                            }
                            else
                            {
                                WriteAttribute("terminus", "n");
                                aminoAcidMass = PeptideMassCalculator.DEFAULT_N_TERMINUS_MASS_CHANGE;
                            }

                            WriteAttributePlusMinus("massdiff", modDef.ModificationMass, 5);          // Mass difference, must begin with + or -
                            WriteAttribute("mass", aminoAcidMass + modDef.ModificationMass, 4);
                            if (modDef.ModificationType == PHRPReader.Data.ModificationDefinition.ResidueModificationType.DynamicMod)
                            {
                                WriteAttribute("variable", "Y");
                            }
                            else
                            {
                                WriteAttribute("variable", "N");
                            }

                            WriteAttribute("symbol", modDef.ModificationSymbol.ToString());              // Symbol used by search-engine to denote this mod
                            if (residue == PHRPReader.Data.AminoAcidModInfo.N_TERMINAL_PROTEIN_SYMBOL_DMS || residue == PHRPReader.Data.AminoAcidModInfo.C_TERMINAL_PROTEIN_SYMBOL_DMS)
                            {
                                // Modification can only occur at the protein terminus
                                WriteAttribute("protein_terminus", "Y");
                            }
                            else
                            {
                                WriteAttribute("protein_terminus", "N");
                            }

                            WriteAttribute("description", modDef.MassCorrectionTag);
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
                foreach (KeyValuePair<string, string> item in SearchEngineParams.Parameters)
                    WriteNameValueElement("parameter", item.Key, item.Value);
            }

            mXMLWriter.WriteEndElement();                    // search_summary
        }

        /// <summary>
        /// Append a spectrum and its PSMs to the .pepXML file
        /// </summary>
        /// <param name="spectrum"></param>
        /// <param name="psms"></param>
        /// <param name="seqToProteinMap"></param>
        public void WriteSpectrum(ref SpectrumInfoType spectrum, List<PHRPReader.Data.PSM> psms, ref SortedList<int, List<PHRPReader.Data.ProteinInfo>> seqToProteinMap)
        {
            double massErrorDa;
            double massErrorPPM;
            double totalMass;
            string collisionMode = string.Empty;

            // The keys in this dictionary are the residue position in the peptide; the values are the total mass (including all mods)
            Dictionary<int, double> modifiedResidues;
            modifiedResidues = new Dictionary<int, double>();
            if (psms is null || psms.Count == 0)
                return;
            {
                var withBlock = mXMLWriter;
                withBlock.WriteStartElement("spectrum_query");
                withBlock.WriteAttributeString("spectrum", spectrum.SpectrumName);         // Example: QC_05_2_05Dec05_Doc_0508-08.9427.9427.1
                WriteAttribute("start_scan", spectrum.StartScan);
                WriteAttribute("end_scan", spectrum.EndScan);
                WriteAttribute("retention_time_sec", spectrum.ElutionTimeMinutes * 60.0, 2);
                if (GetPepXMLCollisionMode(spectrum.CollisionMode, ref collisionMode))
                {
                    WriteAttribute("activation_method", collisionMode);
                }

                WriteAttribute("precursor_neutral_mass", spectrum.PrecursorNeutralMass);
                WriteAttribute("assumed_charge", spectrum.AssumedCharge);
                WriteAttribute("index", spectrum.Index);
                WriteAttribute("spectrumNativeID", spectrum.NativeID);            // Example: controllerType=0 controllerNumber=1 scan=20554
                withBlock.WriteStartElement("search_result");
            }

            foreach (PHRPReader.Data.PSM psmEntry in psms)
            {
                mXMLWriter.WriteStartElement("search_hit");
                WriteAttribute("hit_rank", psmEntry.ScoreRank);
                string peptide = string.Empty;
                string cleanSequence;
                string prefix = string.Empty;
                string suffix = string.Empty;
                if (PeptideCleavageStateCalculator.SplitPrefixAndSuffixFromSequence(psmEntry.Peptide, out peptide, out prefix, out suffix))
                {
                    // The peptide sequence needs to be just the amino acids; no mod symbols
                    cleanSequence = PeptideCleavageStateCalculator.ExtractCleanSequenceFromSequenceWithMods(peptide, false);
                    mXMLWriter.WriteAttributeString("peptide", cleanSequence);
                    mXMLWriter.WriteAttributeString("peptide_prev_aa", prefix);
                    mXMLWriter.WriteAttributeString("peptide_next_aa", suffix);
                }
                else
                {
                    cleanSequence = PeptideCleavageStateCalculator.ExtractCleanSequenceFromSequenceWithMods(psmEntry.Peptide, false);
                    mXMLWriter.WriteAttributeString("peptide", cleanSequence);
                    mXMLWriter.WriteAttributeString("peptide_prev_aa", string.Empty);
                    mXMLWriter.WriteAttributeString("peptide_next_aa", string.Empty);
                }

                if (!cleanSequence.Equals(peptide))
                {
                    mXMLWriter.WriteAttributeString("peptide_with_mods", psmEntry.PeptideWithNumericMods);
                }

                mXMLWriter.WriteAttributeString("protein", psmEntry.ProteinFirst);

                // Could optionally write out protein description
                // .WriteAttributeString("protein_descr", searchHit.StrProteinDescription)

                WriteAttribute("num_tot_proteins", psmEntry.Proteins.Count);
                WriteAttribute("num_matched_ions", 0);
                WriteAttribute("tot_num_ions", 0);
                WriteAttribute("calc_neutral_pep_mass", psmEntry.PeptideMonoisotopicMass, 4);
                if (!double.TryParse(psmEntry.MassErrorDa, out massErrorDa))
                {
                    massErrorDa = 0.0;
                }

                WriteAttributePlusMinus("massdiff", massErrorDa, 5);

                // Write the number of tryptic ends (0 for non-tryptic, 1 for partially tryptic, 2 for fully tryptic)
                WriteAttribute("num_tol_term", psmEntry.NumTrypticTermini);
                WriteAttribute("num_missed_cleavages", psmEntry.NumMissedCleavages);

                // Initially all peptides will have "is_rejected" = 0
                WriteAttribute("is_rejected", 0);
                List<PHRPReader.Data.ProteinInfo> proteins = null;
                bool proteinInfoAvailable;
                int numTrypticTermini;
                if (seqToProteinMap is object && seqToProteinMap.Count > 0)
                {
                    proteinInfoAvailable = seqToProteinMap.TryGetValue(psmEntry.SeqID, out proteins);
                }
                else
                {
                    proteinInfoAvailable = false;
                }

                int proteinsWritten = 0;

                // Write out the additional proteins
                foreach (string proteinAddnl in psmEntry.Proteins)
                {
                    if (!proteinAddnl.Equals(psmEntry.ProteinFirst))
                    {
                        mXMLWriter.WriteStartElement("alternative_protein");
                        mXMLWriter.WriteAttributeString("protein", proteinAddnl);
                        // .WriteAttributeString("protein_descr", proteinAddnlDescription)

                        // Initially use .NumTrypticTermini
                        // We'll update this using proteins if possible
                        numTrypticTermini = psmEntry.NumTrypticTermini;
                        if (proteinInfoAvailable)
                        {
                            foreach (var protein in proteins)
                            {
                                if (protein.ProteinName.Equals(proteinAddnl))
                                {
                                    numTrypticTermini = (int)protein.CleavageState;
                                    break;
                                }
                            }
                        }

                        WriteAttribute("num_tol_term", numTrypticTermini);
                        mXMLWriter.WriteEndElement();      // alternative_protein
                    }

                    proteinsWritten += 1;
                    if (MaxProteinsPerPSM > 0 && proteinsWritten >= MaxProteinsPerPSM)
                    {
                        break;
                    }
                }

                if (psmEntry.ModifiedResidues.Count > 0)
                {
                    mXMLWriter.WriteStartElement("modification_info");
                    double nTermAddon = 0.0;
                    double cTermAddon = 0.0;

                    // Look for N and C terminal mods in psmEntry.ModifiedResidues
                    foreach (var residue in psmEntry.ModifiedResidues)
                    {
                        if (residue.ModDefinition.ModificationType == PHRPReader.Data.ModificationDefinition.ResidueModificationType.TerminalPeptideStaticMod || residue.ModDefinition.ModificationType == PHRPReader.Data.ModificationDefinition.ResidueModificationType.ProteinTerminusStaticMod)
                        {
                            switch (residue.TerminusState)
                            {
                                case PHRPReader.Data.AminoAcidModInfo.ResidueTerminusState.PeptideNTerminus:
                                case PHRPReader.Data.AminoAcidModInfo.ResidueTerminusState.ProteinNTerminus:
                                case PHRPReader.Data.AminoAcidModInfo.ResidueTerminusState.ProteinNandCCTerminus:
                                    {
                                        nTermAddon += residue.ModDefinition.ModificationMass;
                                        break;
                                    }

                                case PHRPReader.Data.AminoAcidModInfo.ResidueTerminusState.PeptideCTerminus:
                                case PHRPReader.Data.AminoAcidModInfo.ResidueTerminusState.ProteinCTerminus:
                                    {
                                        cTermAddon += residue.ModDefinition.ModificationMass;
                                        break;
                                    }

                                default:
                                    {
                                        // This is unexpected
                                        OnErrorEvent("Peptide or Protein terminal mod found, but residue is not at a peptide or protein terminus: " + residue.Residue + " at position " + residue.ResidueLocInPeptide + " in peptide " + psmEntry.Peptide + ", scan " + psmEntry.ScanNumber);
                                        break;
                                    }
                            }
                        }
                    }

                    // If a peptide-terminal mod, add either of these attributes:
                    if (Math.Abs(nTermAddon) > float.Epsilon)
                    {
                        WriteAttributePlusMinus("mod_nterm_mass", PeptideMassCalculator.DEFAULT_N_TERMINUS_MASS_CHANGE + nTermAddon, 5);
                    }

                    if (Math.Abs(cTermAddon) > float.Epsilon)
                    {
                        WriteAttributePlusMinus("mod_cterm_mass", PeptideMassCalculator.DEFAULT_C_TERMINUS_MASS_CHANGE + cTermAddon, 5);
                    }

                    // Write out an entry for each modified amino acid
                    // We need to keep track of the total mass of each modified residue (excluding terminal mods) since a residue could have multiple modifications
                    modifiedResidues.Clear();
                    foreach (var residue in psmEntry.ModifiedResidues)
                    {
                        if (!(residue.ModDefinition.ModificationType == PHRPReader.Data.ModificationDefinition.ResidueModificationType.TerminalPeptideStaticMod || residue.ModDefinition.ModificationType == PHRPReader.Data.ModificationDefinition.ResidueModificationType.ProteinTerminusStaticMod))
                        {
                            if (modifiedResidues.TryGetValue(residue.ResidueLocInPeptide, out totalMass))
                            {
                                // This residue has more than one modification applied to it
                                totalMass += residue.ModDefinition.ModificationMass;
                                modifiedResidues[residue.ResidueLocInPeptide] = totalMass;
                            }
                            else
                            {
                                totalMass = mPeptideMassCalculator.GetAminoAcidMass(residue.Residue) + residue.ModDefinition.ModificationMass;
                                modifiedResidues.Add(residue.ResidueLocInPeptide, totalMass);
                            }
                        }
                    }

                    foreach (var item in modifiedResidues)
                    {
                        mXMLWriter.WriteStartElement("mod_aminoacid_mass");
                        WriteAttribute("position", item.Key);     // Position of residue in peptide
                        WriteAttribute("mass", item.Value, 5);    // Total amino acid mass, including all mods (but excluding N or C terminal mods)
                        mXMLWriter.WriteEndElement();      // mod_aminoacid_mass
                    }

                    mXMLWriter.WriteEndElement();      // modification_info
                }

                // Write out the search scores
                foreach (var item in psmEntry.AdditionalScores)
                {
                    if (mPNNLScoreNameMap.TryGetValue(item.Key, out var alternateScoreName))
                    {
                        WriteNameValueElement("search_score", alternateScoreName, item.Value);
                    }
                    else
                    {
                        WriteNameValueElement("search_score", item.Key, item.Value);
                    }
                }

                WriteNameValueElement("search_score", "msgfspecprob", psmEntry.MSGFSpecEValue);

                // Write out the mass error ppm value as a custom search score
                WriteNameValueElement("search_score", "MassErrorPPM", psmEntry.MassErrorPPM);
                if (!double.TryParse(psmEntry.MassErrorPPM, out massErrorPPM))
                {
                    massErrorPPM = 0.0;
                }

                WriteNameValueElement("search_score", "AbsMassErrorPPM", Math.Abs(massErrorPPM), 4);

                // ' Old, unused
                // WritePeptideProphetUsingMSGF(mXMLWriter, searchHit, iNumTrypticTermini, iNumMissedCleavages)

                mXMLWriter.WriteEndElement();              // search_hit
            }

            mXMLWriter.WriteEndElement();            // search_result
            mXMLWriter.WriteEndElement();            // spectrum_query
        }

        // Old, unused
        // Private Sub WritePeptideProphetUsingMSGF(ByRef mXMLWriter As System.Xml.XmlWriter, ByRef searchHit As clsSearchHit, ByVal iNumTrypticTermini As Integer, ByVal iNumMissedCleavages As Integer)

        // Dim msgf As String
        // Dim fval As String

        // With mXMLWriter
        // .WriteStartElement("analysis_result")
        // .WriteAttributeString("analysis", "peptideprophet")

        // .WriteStartElement("peptideprophet_result")
        // msgf = MSGFConversion.MSGFToProbability(searchHit.dMSGFSpecProb).ToString("0.0000")
        // fval = MSGFConversion.MSGFToFValue(searchHit.dMSGFSpecProb).ToString("0.0000")

        // .WriteAttributeString("probability", msgf)
        // .WriteAttributeString("all_ntt_prob", "(" & msgf & "," & msgf & "," & msgf & ")")

        // .WriteStartElement("search_score_summary")

        // .WriteStartElement("parameter")
        // .WriteAttributeString("name", "fval")
        // .WriteAttributeString("value", fval)
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
        // .WriteAttributeString("value", searchHit.dMassdiff.ToString("0.000"))
        // .WriteEndElement()

        // .WriteEndElement()			  ' search_score_summary

        // .WriteEndElement()			  ' peptideprophet_result

        // .WriteEndElement()			  ' analysis_result

        // End With

        // End Sub

        // Old, unused
        // Private Class MSGFConversion

        // ''' <summary>
        // ''' Performs a crude approximation of Probability using a MSGF SpecProb value
        // ''' Converts the MSGF score to base-10 log, adds 6, then converts back to the original scale to obtain an adjusted MSGF SpecProb value
        // ''' For example, if MSGF SpecProb = 1E-13, then the adjusted value is 1E-07
        // ''' Computes Probability as 1 - AdjustedMSGFSpecProb
        // ''' </summary>
        // ''' <param name="msgfSpecProb">MSGF SpecProb to convert</param>
        // ''' <returns>Probability</returns>
        // Public Shared Function MSGFToProbability(msgfSpecProb As Double) As Double
        // Constant LOG_MSGF_ADJUST As Integer = 6

        // Dim logMSGF As Double
        // Dim probability As Double

        // If msgfSpecProb >= 1 Then
        //     probability = 0
        // ElseIf msgfSpecProb <= 0 Then
        //     probability = 1
        // Else
        //     logMSGF = Math.Log(msgfSpecProb, 10) + LOG_MSGF_ADJUST
        //     probability = 1 - Math.Pow(10, logMSGF)

        //     If probability < 0 Then
        //         probability = 0
        //     End If

        //     If probability > 1 Then
        //         probability = 1
        //     End If
        // End If

        // Return probability

        // End Function

        // ''' <summary>
        // ''' Performs a crude approximation of FValue using a MSGF SpecProb value
        // ''' Converts the MSGF score to base-10 log, adds 6, then takes the negative of this result
        // ''' For example, if MSGF SpecProb = 1E-13, then computes: -13 + 6 = -7, then returns 7
        // ''' </summary>
        // ''' <param name="msgfSpecProb">MSGF SpecProb to convert</param>
        // ''' <returns>FValue</returns>
        // Public Shared Function MSGFToFValue(msgfSpecProb As Double) As Double
        // Constant LOG_MSGF_ADJUST As Integer = 6

        // Dim logMSGF As Double
        // Dim fvalue As Double

        // If msgfSpecProb >= 1 Then
        //     fvalue = 0
        // Else
        //     logMSGF = Math.Log(msgfSpecProb, 10) + LOG_MSGF_ADJUST
        //     fvalue = -logMSGF
        // End If

        // Return fvalue

        // End Function

        // End Class
    }
}