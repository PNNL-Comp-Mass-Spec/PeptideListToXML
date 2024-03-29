﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using PHRPReader;
using PHRPReader.Data;
using PHRPReader.Reader;
using PRISM;

namespace PeptideListToXML
{
    /// <summary>
    /// PepXML writer
    /// </summary>
    public class PepXMLWriter : EventNotifier
    {
        // Ignore Spelling: href, stylesheet, xmlns, xsi, xsl, yyyy-MM-ddTHH:mm:ss
        // Ignore Spelling: aminoacid, Da, fval, Inetpub, massd, massdiff, nmc, ntt, peptideprophet, tryptic
        // Ignore Spelling: bscore, deltacn, deltacnstar, hyperscore, msgfspecprob, sprank, spscore, xcorr, yscore

        private readonly Options mOptions;

        private readonly PeptideMassCalculator mPeptideMassCalculator;

        private XmlWriter mXMLWriter;

        // This dictionary maps PNNL-based score names to pep-xml standard score names
        private Dictionary<string, string> mPNNLScoreNameMap;

        /// <summary>
        /// Search engine parameters, read by PHRPReader
        /// </summary>
        public SearchEngineParameters SearchEngineParams { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="outputFilePath">Path to the PepXML file to create</param>
        /// <param name="searchEngineParams">Search engine parameters</param>
        /// <param name="options"></param>
        public PepXMLWriter(string outputFilePath, SearchEngineParameters searchEngineParams, Options options)
        {
            mOptions = options;
            SearchEngineParams = searchEngineParams;

            mPeptideMassCalculator = new PeptideMassCalculator();
            InitializePNNLScoreNameMap();

            try
            {
                InitializePepXMLFile(outputFilePath, options.FastaFilePath);
            }
            catch (Exception ex)
            {
                throw new Exception("Error initializing PepXML file: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Close the pepXML document
        /// </summary>
        public void CloseDocument()
        {
            mXMLWriter.WriteEndElement();                // msms_run_summary
            mXMLWriter.WriteEndElement();                // msms_pipeline_analysis
            mXMLWriter.WriteEndDocument();
            mXMLWriter.Flush();
            mXMLWriter.Close();
        }

        private bool GetPepXMLCollisionMode(string psmCollisionMode, out string pepXMLCollisionMode)
        {
            var collisionModeUCase = psmCollisionMode.ToUpper();
            switch (collisionModeUCase)
            {
                case "CID":
                case "ETD":
                case "HCD":
                    pepXMLCollisionMode = collisionModeUCase;
                    break;

                case "ETD/CID":
                case "ETD-CID":
                    pepXMLCollisionMode = "ETD/CID";
                    break;

                default:
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

            return !string.IsNullOrEmpty(pepXMLCollisionMode);
        }

        /// <summary>
        /// Initialize a Pep.XML file for writing
        /// </summary>
        /// <param name="outputFilePath"></param>
        /// <param name="fastaFilePath"></param>
        private void InitializePepXMLFile(string outputFilePath, string fastaFilePath)
        {
            if (string.IsNullOrWhiteSpace(fastaFilePath))
            {
                fastaFilePath = @"C:\Database\Unknown_Database.fasta";
            }

            var outputFile = new FileInfo(outputFilePath);
            var writerSettings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = false,
                NewLineOnAttributes = false,
                Encoding = Encoding.ASCII
            };

            mXMLWriter = XmlWriter.Create(outputFilePath, writerSettings);

            mXMLWriter.WriteStartDocument();
            mXMLWriter.WriteProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"pepXML_std.xsl\"");
            WriteHeaderElements(outputFile);
            WriteSearchSummary(fastaFilePath);
        }

        private void InitializePNNLScoreNameMap()
        {
            mPNNLScoreNameMap = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase)
            {
                // SEQUEST scores
                {"XCorr", "xcorr"},
                {"DelCn", "deltacn"},
                {"Sp", "spscore"},
                {"DelCn2", "deltacnstar"},
                {"RankSp", "sprank"},
                // X!Tandem scores
                {"Peptide_Hyperscore", "hyperscore"},
                {"Peptide_Expectation_Value", "expect"},
                {"y_score", "yscore"},
                {"b_score", "bscore"}
            };
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

        /// <summary>
        /// Append the value to the XML file, formatting the number so that it begins with a + sign if positive or a - sign if negative
        /// Rounds the number to the specified number of digits, trimming off trailing zeros
        /// Example output: +79.9663 or -17.016
        /// </summary>
        /// <param name="attributeName"></param>
        /// <param name="value"></param>
        /// <param name="digitsOfPrecision"></param>
        private void WriteAttributePlusMinus(string attributeName, double value, byte digitsOfPrecision)
        {
            mXMLWriter.WriteAttributeString(attributeName, SynFileReaderBaseClass.NumToStringPlusMinus(value, digitsOfPrecision));
        }

        /// <summary>
        /// Append the value to the XML file, rounding the number to the specified number of digits
        /// </summary>
        /// <param name="attributeName"></param>
        /// <param name="value"></param>
        /// <param name="digitsAfterDecimal"></param>
        private void WriteAttribute(string attributeName, double value, byte digitsAfterDecimal = 4)
        {
            mXMLWriter.WriteAttributeString(attributeName, StringUtilities.DblToString(value, digitsAfterDecimal));
        }

        private void WriteNameValueElement(string elementName, string name, string value)
        {
            mXMLWriter.WriteStartElement(elementName);
            WriteAttribute("name", name);
            WriteAttribute("value", value);
            mXMLWriter.WriteEndElement();
        }

        private void WriteNameValueElement(string elementName, string name, double value, byte digitsOfPrecision)
        {
            mXMLWriter.WriteStartElement(elementName);
            WriteAttribute("name", name);
            WriteAttribute("value", value, digitsOfPrecision);
            mXMLWriter.WriteEndElement();
        }

        private void WriteHeaderElements(FileSystemInfo outputFile)
        {
            mXMLWriter.WriteStartElement("msms_pipeline_analysis", "http://regis-web.systemsbiology.net/pepXML");
            mXMLWriter.WriteAttributeString("date", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
            mXMLWriter.WriteAttributeString("summary_xml", outputFile.Name);
            mXMLWriter.WriteAttributeString("xmlns", "http://regis-web.systemsbiology.net/pepXML");
            mXMLWriter.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");

            // Old:               ("xsi", "schemaLocation", Nothing, "http://regis-web.systemsbiology.net/pepXML c:\Inetpub\wwwrootpepXML_v113.xsd")
            mXMLWriter.WriteAttributeString("xsi", "schemaLocation", null, "http://sashimi.sourceforge.net/schema_revision/pepXML/pepXML_v117.xsd");
            mXMLWriter.WriteStartElement("analysis_summary");
            var searchDate = SearchEngineParams.SearchDate;
            if (searchDate < new DateTime(1980, 1, 2))
            {
                // Use the date of the input file since the reported SearchDate is invalid
                var sourceFile = new FileInfo(mOptions.InputFilePath);
                if (sourceFile.Exists)
                {
                    searchDate = sourceFile.LastWriteTime;
                }
            }

            mXMLWriter.WriteAttributeString("time", searchDate.ToString("yyyy-MM-ddTHH:mm:ss"));
            mXMLWriter.WriteAttributeString("analysis", SearchEngineParams.SearchEngineName);
            mXMLWriter.WriteAttributeString("version", SearchEngineParams.SearchEngineVersion);
            mXMLWriter.WriteEndElement();
            mXMLWriter.WriteStartElement("msms_run_summary");
            mXMLWriter.WriteAttributeString("base_name", mOptions.DatasetName);
            mXMLWriter.WriteAttributeString("raw_data_type", "raw");
            mXMLWriter.WriteAttributeString("raw_data", ".mzXML");
            mXMLWriter.WriteStartElement("sample_enzyme");
            mXMLWriter.WriteAttributeString("name", SearchEngineParams.Enzyme);

            if (SearchEngineParams.Enzyme.IndexOf("trypsin", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                mXMLWriter.WriteStartElement("specificity");
                mXMLWriter.WriteAttributeString("cut", "KR");
                mXMLWriter.WriteAttributeString("no_cut", "P");
                mXMLWriter.WriteAttributeString("sense", "C");
                mXMLWriter.WriteEndElement();
            }
            else
            {
                // Future: get the specificity info from mSearchEngineParams
                // For now, just use trypsin specificity
                mXMLWriter.WriteStartElement("specificity");
                mXMLWriter.WriteAttributeString("cut", "KR");
                mXMLWriter.WriteAttributeString("no_cut", "P");
                mXMLWriter.WriteAttributeString("sense", "C");
                mXMLWriter.WriteEndElement();
            }

            mXMLWriter.WriteEndElement(); // sample_enzyme
        }

        private void WriteSearchSummary(string fastaFilePath)
        {
            var terminalSymbols = ModificationDefinition.GetTerminalSymbols();
            string targetResidues;
            double aminoAcidMass;

            mXMLWriter.WriteStartElement("search_summary");
            mXMLWriter.WriteAttributeString("base_name", mOptions.DatasetName);
            mXMLWriter.WriteAttributeString("source_file", Path.GetFileName(mOptions.InputFilePath));
            mXMLWriter.WriteAttributeString("search_engine", SearchEngineParams.SearchEngineName);
            mXMLWriter.WriteAttributeString("search_engine_version", SearchEngineParams.SearchEngineVersion);
            mXMLWriter.WriteAttributeString("precursor_mass_type", SearchEngineParams.PrecursorMassType);
            mXMLWriter.WriteAttributeString("fragment_mass_type", SearchEngineParams.FragmentMassType);
            mXMLWriter.WriteAttributeString("search_id", "1");
            mXMLWriter.WriteStartElement("search_database");

            string fastaFilePathToUse;
            if (!string.IsNullOrEmpty(SearchEngineParams.FastaFilePath))
            {
                try
                {
                    // Update the directory to start with C:\Database
                    fastaFilePathToUse = Path.Combine(@"C:\Database", Path.GetFileName(SearchEngineParams.FastaFilePath));
                }
                catch (Exception)
                {
                    fastaFilePathToUse = SearchEngineParams.FastaFilePath;
                }
            }
            else
            {
                fastaFilePathToUse = string.Copy(fastaFilePath);
            }

            mXMLWriter.WriteAttributeString("local_path", fastaFilePathToUse);
            mXMLWriter.WriteAttributeString("type", "AA");
            mXMLWriter.WriteEndElement(); // search_database

            mXMLWriter.WriteStartElement("enzymatic_search_constraint");
            WriteAttribute("enzyme", SearchEngineParams.Enzyme);
            WriteAttribute("max_num_internal_cleavages", SearchEngineParams.MaxNumberInternalCleavages);
            WriteAttribute("min_number_termini", SearchEngineParams.MinNumberTermini);
            mXMLWriter.WriteEndElement();        // enzymatic_search_constraint

            // Amino acid mod details
            foreach (var modDef in SearchEngineParams.ModList)
            {
                if (!modDef.CanAffectPeptideResidues())
                {
                    continue;
                }

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
                    if (terminalSymbols.Contains(residue))
                    {
                        continue;
                    }

                    mXMLWriter.WriteStartElement("aminoacid_modification");
                    WriteAttribute("aminoacid", residue.ToString());                 // Amino acid symbol, e.g. A
                    WriteAttributePlusMinus("massdiff", modDef.ModificationMass, 5); // Mass difference, must begin with + or -
                    aminoAcidMass = mPeptideMassCalculator.GetAminoAcidMass(residue);
                    WriteAttribute("mass", aminoAcidMass + modDef.ModificationMass);

                    if (modDef.ModificationType == ModificationDefinition.ResidueModificationType.DynamicMod)
                    {
                        WriteAttribute("variable", "Y");
                    }
                    else
                    {
                        WriteAttribute("variable", "N");
                    }

                    WriteAttribute("symbol", modDef.ModificationSymbol.ToString()); // Symbol used by search-engine to denote this mod
                    WriteAttribute("description", modDef.MassCorrectionTag);
                    mXMLWriter.WriteEndElement(); // aminoacid_modification
                }
            }

            // Protein/Peptide terminal mods
            foreach (var modDef in SearchEngineParams.ModList)
            {
                if (!modDef.CanAffectPeptideOrProteinTerminus())
                {
                    continue;
                }

                if (string.IsNullOrEmpty(modDef.TargetResidues))
                {
                    // Target residues should not be empty for terminal mods
                    // But, we'll list them anyway
                    targetResidues = string.Format("{0}{1}",
                        AminoAcidModInfo.N_TERMINAL_PEPTIDE_SYMBOL_DMS,
                        AminoAcidModInfo.C_TERMINAL_PEPTIDE_SYMBOL_DMS);
                }
                else
                {
                    targetResidues = modDef.TargetResidues;
                }

                foreach (var residue in targetResidues)
                {
                    if (!terminalSymbols.Contains(residue))
                    {
                        continue;
                    }

                    mXMLWriter.WriteStartElement("terminal_modification");
                    if (residue == AminoAcidModInfo.C_TERMINAL_PEPTIDE_SYMBOL_DMS || residue == AminoAcidModInfo.C_TERMINAL_PROTEIN_SYMBOL_DMS)
                    {
                        WriteAttribute("terminus", "c");
                        aminoAcidMass = PeptideMassCalculator.DEFAULT_C_TERMINUS_MASS_CHANGE;
                    }
                    else
                    {
                        WriteAttribute("terminus", "n");
                        aminoAcidMass = PeptideMassCalculator.DEFAULT_N_TERMINUS_MASS_CHANGE;
                    }

                    WriteAttributePlusMinus("massdiff", modDef.ModificationMass, 5); // Mass difference, must begin with + or -
                    WriteAttribute("mass", aminoAcidMass + modDef.ModificationMass);

                    if (modDef.ModificationType == ModificationDefinition.ResidueModificationType.DynamicMod)
                    {
                        WriteAttribute("variable", "Y");
                    }
                    else
                    {
                        WriteAttribute("variable", "N");
                    }

                    WriteAttribute("symbol", modDef.ModificationSymbol.ToString()); // Symbol used by search-engine to denote this mod
                    if (residue == AminoAcidModInfo.N_TERMINAL_PROTEIN_SYMBOL_DMS || residue == AminoAcidModInfo.C_TERMINAL_PROTEIN_SYMBOL_DMS)
                    {
                        // Modification can only occur at the protein terminus
                        WriteAttribute("protein_terminus", "Y");
                    }
                    else
                    {
                        WriteAttribute("protein_terminus", "N");
                    }

                    WriteAttribute("description", modDef.MassCorrectionTag);
                    mXMLWriter.WriteEndElement(); // terminal_modification
                }
            }

            // Parameters specific to the search engine
            if (SearchEngineParams.Parameters is null || SearchEngineParams.Parameters.Count == 0)
            {
                if (Math.Abs(SearchEngineParams.PrecursorMassToleranceDa) < float.Epsilon && Math.Abs(SearchEngineParams.PrecursorMassTolerancePpm) < float.Epsilon)
                {
                    // Write out two dummy-parameters
                    mXMLWriter.WriteComment("Dummy search-engine parameters");
                    WriteNameValueElement("parameter", "peptide_mass_tol", "3.000");
                    WriteNameValueElement("parameter", "fragment_ion_tol", "0.000");
                }
                else
                {
                    mXMLWriter.WriteComment("Search-engine parameters");
                    if (SearchEngineParams.PrecursorMassTolerancePpm > 0)
                    {
                        WriteNameValueElement("parameter", "peptide_mass_tol", StringUtilities.DblToString(SearchEngineParams.PrecursorMassTolerancePpm, 2));
                        WriteNameValueElement("parameter", "peptide_mass_tol_units", "ppm");
                    }
                    else if (SearchEngineParams.PrecursorMassToleranceDa > 0)
                    {
                        WriteNameValueElement("parameter", "peptide_mass_tol", StringUtilities.DblToString(SearchEngineParams.PrecursorMassToleranceDa, 6));
                        WriteNameValueElement("parameter", "peptide_mass_tol_units", "Da");
                    }
                }
            }
            else
            {
                mXMLWriter.WriteComment("Search-engine parameters");

                // Write out the search-engine parameters
                foreach (var item in SearchEngineParams.Parameters)
                {
                    WriteNameValueElement("parameter", item.Key, item.Value);
                }
            }

            mXMLWriter.WriteEndElement();                    // search_summary
        }

        /// <summary>
        /// Append a spectrum and its PSMs to the .pepXML file
        /// </summary>
        /// <param name="spectrum"></param>
        /// <param name="psms"></param>
        /// <param name="seqToProteinMap"></param>
        public void WriteSpectrum(SpectrumInfo spectrum, List<PSM> psms, SortedList<int, List<ProteinInfo>> seqToProteinMap)
        {
            // The keys in this dictionary are the residue position in the peptide; the values are the total mass (including all mods)
            var modifiedResidues = new Dictionary<int, double>();

            if (psms is null || psms.Count == 0)
            {
                return;
            }

            mXMLWriter.WriteStartElement("spectrum_query");
            mXMLWriter.WriteAttributeString("spectrum", spectrum.SpectrumTitle); // Example: QC_05_2_05Dec05_Doc_0508-08.9427.9427.1
            WriteAttribute("start_scan", spectrum.StartScan);
            WriteAttribute("end_scan", spectrum.EndScan);
            WriteAttribute("retention_time_sec", spectrum.ElutionTimeMinutes * 60.0, 2);

            if (GetPepXMLCollisionMode(spectrum.CollisionMode, out var collisionMode))
            {
                WriteAttribute("activation_method", collisionMode);
            }

            WriteAttribute("precursor_neutral_mass", spectrum.PrecursorNeutralMass);
            WriteAttribute("assumed_charge", spectrum.AssumedCharge);
            WriteAttribute("index", spectrum.Index);
            WriteAttribute("spectrumNativeID", spectrum.NativeID); // Example: controllerType=0 controllerNumber=1 scan=20554
            mXMLWriter.WriteStartElement("search_result");

            var hasMsgfSpecEValue = psms.Any(psmEntry => !string.IsNullOrWhiteSpace(psmEntry.MSGFSpecEValue));

            foreach (var psmEntry in psms)
            {
                mXMLWriter.WriteStartElement("search_hit");
                WriteAttribute("hit_rank", psmEntry.ScoreRank);
                string cleanSequence;

                if (PeptideCleavageStateCalculator.SplitPrefixAndSuffixFromSequence(psmEntry.Peptide, out var peptide, out var prefix, out var suffix))
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
                WriteAttribute("calc_neutral_pep_mass", psmEntry.PeptideMonoisotopicMass);

                if (!double.TryParse(psmEntry.MassErrorDa, out var massErrorDa))
                {
                    massErrorDa = 0.0;
                }

                WriteAttributePlusMinus("massdiff", massErrorDa, 5);

                // Write the number of tryptic ends (0 for non-tryptic, 1 for partially tryptic, 2 for fully tryptic)
                WriteAttribute("num_tol_term", psmEntry.NumTrypticTermini);
                WriteAttribute("num_missed_cleavages", psmEntry.NumMissedCleavages);

                // Initially all peptides will have "is_rejected" = 0
                WriteAttribute("is_rejected", 0);
                List<ProteinInfo> proteins;
                bool proteinInfoAvailable;

                if (seqToProteinMap.Count > 0)
                {
                    proteinInfoAvailable = seqToProteinMap.TryGetValue(psmEntry.SeqID, out proteins);
                }
                else
                {
                    proteins = new List<ProteinInfo>();
                    proteinInfoAvailable = false;
                }

                var proteinsWritten = 0;

                // Write out the additional proteins
                foreach (var proteinAddnl in psmEntry.Proteins)
                {
                    if (!proteinAddnl.Equals(psmEntry.ProteinFirst))
                    {
                        mXMLWriter.WriteStartElement("alternative_protein");
                        mXMLWriter.WriteAttributeString("protein", proteinAddnl);
                        // .WriteAttributeString("protein_descr", proteinAddnlDescription)

                        // Initially use .NumTrypticTermini
                        // We'll update this using proteins if possible
                        var numTrypticTermini = psmEntry.NumTrypticTermini;

                        if (proteinInfoAvailable)
                        {
                            foreach (var protein in proteins)
                            {
                                if (protein.ProteinName.Equals(proteinAddnl))
                                {
                                    numTrypticTermini = (short)protein.CleavageState;
                                    break;
                                }
                            }
                        }

                        WriteAttribute("num_tol_term", numTrypticTermini);
                        mXMLWriter.WriteEndElement();      // alternative_protein
                    }

                    proteinsWritten++;
                    if (mOptions.MaxProteinsPerPSM > 0 && proteinsWritten >= mOptions.MaxProteinsPerPSM)
                    {
                        break;
                    }
                }

                if (psmEntry.ModifiedResidues.Count > 0)
                {
                    WriteModificationInfo(modifiedResidues, psmEntry);
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

                if (hasMsgfSpecEValue)
                {
                    WriteNameValueElement("search_score", "msgfspecprob", psmEntry.MSGFSpecEValue);
                }

                // Write out the mass error ppm value as a custom search score
                WriteNameValueElement("search_score", "MassErrorPPM", psmEntry.MassErrorPPM);
                if (!double.TryParse(psmEntry.MassErrorPPM, out var massErrorPPM))
                {
                    massErrorPPM = 0.0;
                }

                WriteNameValueElement("search_score", "AbsMassErrorPPM", Math.Abs(massErrorPPM), 4);

                // Old, unused
                // WritePeptideProphetUsingMSGF(mXMLWriter, searchHit, iNumTrypticTermini, iNumMissedCleavages)

                mXMLWriter.WriteEndElement();              // search_hit
            }

            mXMLWriter.WriteEndElement();            // search_result
            mXMLWriter.WriteEndElement();            // spectrum_query
        }

        private void WriteModificationInfo(IDictionary<int, double> modifiedResidues, PSM psmEntry)
        {
            mXMLWriter.WriteStartElement("modification_info");
            var nTermAddon = 0.0;
            var cTermAddon = 0.0;

            // Look for N and C terminal mods in psmEntry.ModifiedResidues
            foreach (var residue in psmEntry.ModifiedResidues)
            {
                if (residue.ModDefinition.ModificationType == ModificationDefinition.ResidueModificationType.TerminalPeptideStaticMod || residue.ModDefinition.ModificationType == ModificationDefinition.ResidueModificationType.ProteinTerminusStaticMod)
                {
                    switch (residue.TerminusState)
                    {
                        case AminoAcidModInfo.ResidueTerminusState.PeptideNTerminus:
                        case AminoAcidModInfo.ResidueTerminusState.ProteinNTerminus:
                        case AminoAcidModInfo.ResidueTerminusState.ProteinNandCCTerminus:
                            nTermAddon += residue.ModDefinition.ModificationMass;
                            break;

                        case AminoAcidModInfo.ResidueTerminusState.PeptideCTerminus:
                        case AminoAcidModInfo.ResidueTerminusState.ProteinCTerminus:
                            cTermAddon += residue.ModDefinition.ModificationMass;
                            break;

                        default:
                            // This is unexpected
                            OnErrorEvent("Peptide or Protein terminal mod found, but residue is not at a peptide or protein terminus: " + residue.Residue + " at position " + residue.ResidueLocInPeptide + " in peptide " + psmEntry.Peptide + ", scan " + psmEntry.ScanNumber);
                            break;
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
                if (residue.ModDefinition.ModificationType is
                    ModificationDefinition.ResidueModificationType.TerminalPeptideStaticMod or
                    ModificationDefinition.ResidueModificationType.ProteinTerminusStaticMod)
                {
                    continue;
                }

                if (modifiedResidues.TryGetValue(residue.ResidueLocInPeptide, out var totalMass))
                {
                    // This residue has more than one modification applied to it
                    totalMass += residue.ModDefinition.ModificationMass;
                    modifiedResidues[residue.ResidueLocInPeptide] = totalMass;
                }
                else
                {
                    var totalMass2 = mPeptideMassCalculator.GetAminoAcidMass(residue.Residue) + residue.ModDefinition.ModificationMass;
                    modifiedResidues.Add(residue.ResidueLocInPeptide, totalMass2);
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
    }
}
