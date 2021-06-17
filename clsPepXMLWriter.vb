﻿Option Strict On

Imports PHRPReader
Imports System.IO
Imports System.Text
Imports System.Xml
Imports PRISM

Public Class clsPepXMLWriter
    Inherits EventNotifier

    ' Ignore Spelling: href, stylesheet, xmlns, xsi, xsl, yyyy-MM-ddTHH:mm:ss
    ' Ignore Spelling: aminoacid, fval, Inetpub, massd, massdiff, nmc, ntt, peptideprophet, tryptic
    ' Ignore Spelling: bscore, deltacn, deltacnstar, hyperscore, msgfspecprob, sprank, spscore, xcorr, yscore

#Region "Structures"

    Public Structure udtSpectrumInfoType
        Public SpectrumName As String           ' Spectrum Title: could be "QC_05_2_05Dec05_Doc_0508-08.9427.9427.1" or just "scan=16134 cs=2"
        Public StartScan As Integer
        Public EndScan As Integer
        Public PrecursorNeutralMass As Double
        Public AssumedCharge As Integer
        Public ElutionTimeMinutes As Double
        Public CollisionMode As String
        Public Index As Integer
        Public NativeID As String
    End Structure

#End Region

#Region "Module-wide variables"
    Private ReadOnly mPeptideMassCalculator As PeptideMassCalculator

    Private mXMLWriter As XmlWriter

    Private mFileOpen As Boolean

    ' This dictionary maps PNNL-based score names to pep-xml standard score names
    Private mPNNLScoreNameMap As Dictionary(Of String, String)
#End Region

#Region "Properties"

    ' ReSharper disable once UnusedMember.Global
    Public ReadOnly Property DatasetName As String

    Public ReadOnly Property IsWritable As Boolean
        Get
            Return mFileOpen
        End Get
    End Property

    Public Property MaxProteinsPerPSM As Integer

    ' ReSharper disable once UnusedMember.Global
    Public ReadOnly Property SearchEngineParams As Data.SearchEngineParameters

    ' ReSharper disable once UnusedMember.Global
    Public ReadOnly Property SourceFilePath As String
#End Region

    ''' <summary>
    ''' Instantiate a new PepXML writer
    ''' </summary>
    ''' <param name="strDatasetName">Name of the Dataset</param>
    ''' <param name="strFastaFilePath">Fasta file path to use if objSearchEngineParams.FastaFilePath is empty</param>
    ''' <param name="objSearchEngineParams">Search engine parameters</param>
    ''' <param name="strSourceFilePath">Source file path</param>
    ''' <param name="strOutputFilePath">Path to the PepXML file to create</param>
    ''' <remarks></remarks>
    Public Sub New(strDatasetName As String, strFastaFilePath As String,
                   objSearchEngineParams As PHRPReader.Data.SearchEngineParameters,
                   strSourceFilePath As String, strOutputFilePath As String)

        SearchEngineParams = objSearchEngineParams
        DatasetName = strDatasetName
        If DatasetName Is Nothing Then DatasetName = "Unknown"

        If String.IsNullOrEmpty(strSourceFilePath) Then strSourceFilePath = String.Empty
        SourceFilePath = strSourceFilePath

        mPeptideMassCalculator = New PeptideMassCalculator()

        InitializePNNLScoreNameMap()

        MaxProteinsPerPSM = 0

        Try
            If Not InitializePepXMLFile(strOutputFilePath, strFastaFilePath) Then
                Throw New Exception("Error initializing PepXML file")
            End If

        Catch ex As Exception
            Throw New Exception("Error initializing PepXML file: " & ex.Message, ex)
        End Try

    End Sub

    Public Function CloseDocument() As Boolean
        If Not mFileOpen Then
            Return False
        End If

        mXMLWriter.WriteEndElement()                ' msms_run_summary

        mXMLWriter.WriteEndElement()                ' msms_pipeline_analysis
        mXMLWriter.WriteEndDocument()
        mXMLWriter.Flush()
        mXMLWriter.Close()

        Return True
    End Function

    Private Function GetPepXMLCollisionMode(strPSMCollisionMode As String, ByRef strPepXMLCollisionMode As String) As Boolean

        Dim strCollisionModeUCase As String = strPSMCollisionMode.ToUpper()

        Select Case strCollisionModeUCase
            Case "CID", "ETD", "HCD"
                strPepXMLCollisionMode = strCollisionModeUCase
            Case "ETD/CID", "ETD-CID"
                strPepXMLCollisionMode = "ETD/CID"
            Case Else
                If strCollisionModeUCase.StartsWith("CID") Then
                    strPepXMLCollisionMode = "CID"
                ElseIf strCollisionModeUCase.StartsWith("HCD") Then
                    strPepXMLCollisionMode = "HCD"
                ElseIf strCollisionModeUCase.StartsWith("ETD") Then
                    strPepXMLCollisionMode = "ETD"
                Else
                    strPepXMLCollisionMode = String.Empty
                End If
        End Select

        If String.IsNullOrEmpty(strPepXMLCollisionMode) Then
            Return False
        Else
            Return True
        End If

    End Function

    ''' <summary>
    ''' Initialize a Pep.XML file for writing
    ''' </summary>
    ''' <param name="strOutputFilePath"></param>
    ''' <param name="strFastaFilePath"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function InitializePepXMLFile(strOutputFilePath As String, strFastaFilePath As String) As Boolean

        Dim fiOutputFile As FileInfo

        If String.IsNullOrWhiteSpace(strFastaFilePath) Then
            strFastaFilePath = "C:\Database\Unknown_Database.fasta"
        End If

        fiOutputFile = New FileInfo(strOutputFilePath)

        Dim oSettings = New XmlWriterSettings()
        oSettings.Indent = True
        oSettings.OmitXmlDeclaration = False
        oSettings.NewLineOnAttributes = False
        oSettings.Encoding = Encoding.ASCII

        mXMLWriter = XmlWriter.Create(strOutputFilePath, oSettings)
        mFileOpen = True

        mXMLWriter.WriteStartDocument()
        mXMLWriter.WriteProcessingInstruction("xml-stylesheet", "type=""text/xsl"" href=""pepXML_std.xsl""")

        WriteHeaderElements(fiOutputFile)

        WriteSearchSummary(strFastaFilePath)

        Return True

    End Function

    Private Sub InitializePNNLScoreNameMap()
        mPNNLScoreNameMap = New Dictionary(Of String, String)(StringComparer.CurrentCultureIgnoreCase)

        ' SEQUEST scores
        mPNNLScoreNameMap.Add("XCorr", "xcorr")
        mPNNLScoreNameMap.Add("DelCn", "deltacn")
        mPNNLScoreNameMap.Add("Sp", "spscore")
        mPNNLScoreNameMap.Add("DelCn2", "deltacnstar")
        mPNNLScoreNameMap.Add("RankSp", "sprank")

        ' X!Tandem scores
        mPNNLScoreNameMap.Add("Peptide_Hyperscore", "hyperscore")
        mPNNLScoreNameMap.Add("Peptide_Expectation_Value", "expect")
        mPNNLScoreNameMap.Add("y_score", "yscore")
        mPNNLScoreNameMap.Add("b_score", "bscore")

    End Sub

    Private Sub WriteAttribute(strAttributeName As String, Value As String)
        If String.IsNullOrEmpty(Value) Then
            mXMLWriter.WriteAttributeString(strAttributeName, String.Empty)
        Else
            mXMLWriter.WriteAttributeString(strAttributeName, Value)
        End If
    End Sub

    Private Sub WriteAttribute(strAttributeName As String, Value As Integer)
        mXMLWriter.WriteAttributeString(strAttributeName, Value.ToString())
    End Sub

    ' ReSharper disable once UnusedMember.Local
    Private Sub WriteAttribute(strAttributeName As String, Value As Single)
        WriteAttribute(strAttributeName, Value, DigitsOfPrecision:=4)
    End Sub

    Private Sub WriteAttribute(strAttributeName As String, Value As Single, DigitsOfPrecision As Integer)
        Dim strFormatString = "0"
        If DigitsOfPrecision > 0 Then
            strFormatString &= "." & New String("0"c, DigitsOfPrecision)
        End If

        mXMLWriter.WriteAttributeString(strAttributeName, Value.ToString(strFormatString))
    End Sub

    Private Sub WriteAttributePlusMinus(strAttributeName As String, Value As Double, DigitsOfPrecision As Integer)
        mXMLWriter.WriteAttributeString(strAttributeName, Reader.SynFileReaderBaseClass.NumToStringPlusMinus(Value, DigitsOfPrecision))
    End Sub

    Private Sub WriteAttribute(strAttributeName As String, Value As Double)
        WriteAttribute(strAttributeName, Value, DigitsOfPrecision:=4)
    End Sub

    Private Sub WriteAttribute(strAttributeName As String, Value As Double, DigitsOfPrecision As Integer)
        Dim strFormatString = "0"
        If DigitsOfPrecision > 0 Then
            strFormatString &= "." & New String("0"c, DigitsOfPrecision)
        End If

        mXMLWriter.WriteAttributeString(strAttributeName, Value.ToString(strFormatString))
    End Sub

    Private Sub WriteNameValueElement(strElementName As String, strName As String, Value As String)
        mXMLWriter.WriteStartElement(strElementName)
        WriteAttribute("name", strName)
        WriteAttribute("value", Value)
        mXMLWriter.WriteEndElement()
    End Sub

    Private Sub WriteNameValueElement(strElementName As String, strName As String, Value As Double, DigitsOfPrecision As Integer)
        mXMLWriter.WriteStartElement(strElementName)
        WriteAttribute("name", strName)
        WriteAttribute("value", Value, DigitsOfPrecision)
        mXMLWriter.WriteEndElement()
    End Sub

    Private Sub WriteHeaderElements(fiOutputFile As FileSystemInfo)

        Dim dtSearchDate As DateTime

        With mXMLWriter

            .WriteStartElement("msms_pipeline_analysis", "http://regis-web.systemsbiology.net/pepXML")
            .WriteAttributeString("date", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"))
            .WriteAttributeString("summary_xml", fiOutputFile.Name)
            .WriteAttributeString("xmlns", "http://regis-web.systemsbiology.net/pepXML")
            .WriteAttributeString("xmlns", "xsi", Nothing, "http://www.w3.org/2001/XMLSchema-instance")

            ' Old:               ("xsi", "schemaLocation", Nothing, "http://regis-web.systemsbiology.net/pepXML c:\Inetpub\wwwrootpepXML_v113.xsd")
            .WriteAttributeString("xsi", "schemaLocation", Nothing, "http://sashimi.sourceforge.net/schema_revision/pepXML/pepXML_v117.xsd")

            .WriteStartElement("analysis_summary")

            dtSearchDate = SearchEngineParams.SearchDate
            If dtSearchDate < New DateTime(1980, 1, 2) Then
                ' Use the date of the input file since the reported SearchDate is invalid
                Dim fiSourceFile = New FileInfo(SourceFilePath)
                If fiSourceFile.Exists Then
                    dtSearchDate = fiSourceFile.LastWriteTime
                End If
            End If

            .WriteAttributeString("time", dtSearchDate.ToString("yyyy-MM-ddTHH:mm:ss"))
            .WriteAttributeString("analysis", SearchEngineParams.SearchEngineName)
            .WriteAttributeString("version", SearchEngineParams.SearchEngineVersion)
            .WriteEndElement()

            .WriteStartElement("msms_run_summary")

            .WriteAttributeString("base_name", DatasetName)
            .WriteAttributeString("raw_data_type", "raw")
            .WriteAttributeString("raw_data", ".mzXML")

            .WriteStartElement("sample_enzyme")
            .WriteAttributeString("name", SearchEngineParams.Enzyme)

            ' ToDo: get the specificity info from mSearchEngineParams

            If SearchEngineParams.Enzyme.ToLower().Contains("trypsin") Then
                .WriteStartElement("specificity")
                .WriteAttributeString("cut", "KR")
                .WriteAttributeString("no_cut", "P")
                .WriteAttributeString("sense", "C")
                .WriteEndElement()
            Else
                .WriteStartElement("specificity")
                .WriteAttributeString("cut", "KR")
                .WriteAttributeString("no_cut", "P")
                .WriteAttributeString("sense", "C")
                .WriteEndElement()

            End If

            .WriteEndElement()                  ' sample_enzyme

        End With

    End Sub

    Private Sub WriteSearchSummary(strFastaFilePath As String)
        Dim lstTerminalSymbols As SortedSet(Of Char)
        lstTerminalSymbols = Data.ModificationDefinition.GetTerminalSymbols()

        Dim strFastaFilePathToUse As String
        Dim strTargetResidues As String
        Dim dblAAMass As Double

        With mXMLWriter

            .WriteStartElement("search_summary")

            .WriteAttributeString("base_name", DatasetName)
            .WriteAttributeString("source_file", Path.GetFileName(SourceFilePath))

            .WriteAttributeString("search_engine", SearchEngineParams.SearchEngineName)
            .WriteAttributeString("search_engine_version", SearchEngineParams.SearchEngineVersion)
            .WriteAttributeString("precursor_mass_type", SearchEngineParams.PrecursorMassType)
            .WriteAttributeString("fragment_mass_type", SearchEngineParams.FragmentMassType)

            .WriteAttributeString("search_id", "1")

            .WriteStartElement("search_database")

            If Not String.IsNullOrEmpty(SearchEngineParams.FastaFilePath) Then
                Try
                    ' Update the folder to be the start with C:\Database
                    strFastaFilePathToUse = Path.Combine("C:\Database", Path.GetFileName(SearchEngineParams.FastaFilePath))
                Catch ex As Exception
                    strFastaFilePathToUse = SearchEngineParams.FastaFilePath
                End Try
            Else
                strFastaFilePathToUse = String.Copy(strFastaFilePath)
            End If
            .WriteAttributeString("local_path", strFastaFilePathToUse)

            .WriteAttributeString("type", "AA")
            .WriteEndElement()      ' search_database
        End With

        mXMLWriter.WriteStartElement("enzymatic_search_constraint")
        WriteAttribute("enzyme", SearchEngineParams.Enzyme)
        WriteAttribute("max_num_internal_cleavages", SearchEngineParams.MaxNumberInternalCleavages)
        WriteAttribute("min_number_termini", SearchEngineParams.MinNumberTermini)
        mXMLWriter.WriteEndElement()        ' enzymatic_search_constraint

        ' Amino acid mod details
        For Each objModDef As Data.ModificationDefinition In SearchEngineParams.ModList
            If objModDef.CanAffectPeptideResidues() Then

                If String.IsNullOrEmpty(objModDef.TargetResidues) Then
                    ' This modification can affect any amino acid (skip BJOUXZ)
                    strTargetResidues = "ACDEFGHIKLMNPQRSTVWY"
                Else
                    strTargetResidues = objModDef.TargetResidues
                End If

                For Each chChar In strTargetResidues
                    If Not lstTerminalSymbols.Contains(chChar) Then

                        mXMLWriter.WriteStartElement("aminoacid_modification")

                        WriteAttribute("aminoacid", chChar)                         ' Amino acid symbol, e.g. A
                        WriteAttributePlusMinus("massdiff", objModDef.ModificationMass, 5)          ' Mass difference, must begin with + or -

                        dblAAMass = mPeptideMassCalculator.GetAminoAcidMass(chChar)

                        WriteAttribute("mass", dblAAMass + objModDef.ModificationMass, 4)

                        If objModDef.ModificationType = Data.ModificationDefinition.ResidueModificationType.DynamicMod Then
                            WriteAttribute("variable", "Y")
                        Else
                            WriteAttribute("variable", "N")
                        End If
                        WriteAttribute("symbol", objModDef.ModificationSymbol)              ' Symbol used by search-engine to denote this mod

                        WriteAttribute("description", objModDef.MassCorrectionTag)

                        mXMLWriter.WriteEndElement()        ' aminoacid_modification

                    End If
                Next

            End If
        Next

        ' Protein/Peptide terminal mods
        For Each objModDef As Data.ModificationDefinition In SearchEngineParams.ModList
            If objModDef.CanAffectPeptideOrProteinTerminus() Then

                If String.IsNullOrEmpty(objModDef.TargetResidues) Then
                    ' Target residues should not be empty for terminal mods
                    ' But, we'll list them anyway
                    strTargetResidues = Data.AminoAcidModInfo.N_TERMINAL_PEPTIDE_SYMBOL_DMS & Data.AminoAcidModInfo.C_TERMINAL_PEPTIDE_SYMBOL_DMS
                Else
                    strTargetResidues = objModDef.TargetResidues
                End If

                For Each chChar In strTargetResidues
                    If lstTerminalSymbols.Contains(chChar) Then

                        mXMLWriter.WriteStartElement("terminal_modification")

                        If chChar = Data.AminoAcidModInfo.C_TERMINAL_PEPTIDE_SYMBOL_DMS OrElse chChar = Data.AminoAcidModInfo.C_TERMINAL_PROTEIN_SYMBOL_DMS Then
                            WriteAttribute("terminus", "c")
                            dblAAMass = PeptideMassCalculator.DEFAULT_C_TERMINUS_MASS_CHANGE
                        Else
                            WriteAttribute("terminus", "n")
                            dblAAMass = PeptideMassCalculator.DEFAULT_N_TERMINUS_MASS_CHANGE
                        End If

                        WriteAttributePlusMinus("massdiff", objModDef.ModificationMass, 5)          ' Mass difference, must begin with + or -
                        WriteAttribute("mass", dblAAMass + objModDef.ModificationMass, 4)

                        If objModDef.ModificationType = Data.ModificationDefinition.ResidueModificationType.DynamicMod Then
                            WriteAttribute("variable", "Y")
                        Else
                            WriteAttribute("variable", "N")
                        End If

                        WriteAttribute("symbol", objModDef.ModificationSymbol)              ' Symbol used by search-engine to denote this mod

                        If chChar = Data.AminoAcidModInfo.N_TERMINAL_PROTEIN_SYMBOL_DMS OrElse chChar = Data.AminoAcidModInfo.C_TERMINAL_PROTEIN_SYMBOL_DMS Then
                            ' Modification can only occur at the protein terminus
                            WriteAttribute("protein_terminus", "Y")
                        Else
                            WriteAttribute("protein_terminus", "N")
                        End If

                        WriteAttribute("description", objModDef.MassCorrectionTag)

                        mXMLWriter.WriteEndElement()        ' terminal_modification

                    End If
                Next

            End If
        Next

        ' Parameters specific to the search engine
        If SearchEngineParams.Parameters Is Nothing OrElse SearchEngineParams.Parameters.Count = 0 Then
            ' Write out two dummy-parameters
            mXMLWriter.WriteComment("Dummy search-engine parameters")

            WriteNameValueElement("parameter", "peptide_mass_tol", "3.000")
            WriteNameValueElement("parameter", "fragment_ion_tol", "0.000")
        Else

            mXMLWriter.WriteComment("Search-engine parameters")

            ' Write out the search-engine parameters
            For Each objItem As KeyValuePair(Of String, String) In SearchEngineParams.Parameters
                WriteNameValueElement("parameter", objItem.Key, objItem.Value)
            Next
        End If

        mXMLWriter.WriteEndElement()                    ' search_summary

    End Sub

    Public Sub WriteSpectrum(ByRef objSpectrum As udtSpectrumInfoType, ByRef lstHits As List(Of PHRPReader.Data.PSM), ByRef lstSeqToProteinMap As SortedList(Of Integer, List(Of PHRPReader.Data.ProteinInfo)))

        Dim dblMassErrorDa As Double
        Dim dblMassErrorPPM As Double
        Dim dblTotalMass As Double

        Dim strAlternateScoreName As String = String.Empty
        Dim strCollisionMode As String = String.Empty

        ' The keys in this dictionary are the residue position in the peptide; the values are the total mass (including all mods)
        Dim lstModifiedResidues As Dictionary(Of Integer, Double)
        lstModifiedResidues = New Dictionary(Of Integer, Double)

        If lstHits Is Nothing OrElse lstHits.Count = 0 Then Exit Sub

        With mXMLWriter

            .WriteStartElement("spectrum_query")
            .WriteAttributeString("spectrum", objSpectrum.SpectrumName)         ' Example: QC_05_2_05Dec05_Doc_0508-08.9427.9427.1
            WriteAttribute("start_scan", objSpectrum.StartScan)
            WriteAttribute("end_scan", objSpectrum.EndScan)
            WriteAttribute("retention_time_sec", objSpectrum.ElutionTimeMinutes * 60.0, 2)

            If GetPepXMLCollisionMode(objSpectrum.CollisionMode, strCollisionMode) Then
                WriteAttribute("activation_method", strCollisionMode)
            End If

            WriteAttribute("precursor_neutral_mass", objSpectrum.PrecursorNeutralMass)

            WriteAttribute("assumed_charge", objSpectrum.AssumedCharge)
            WriteAttribute("index", objSpectrum.Index)

            WriteAttribute("spectrumNativeID", objSpectrum.NativeID)            ' Example: controllerType=0 controllerNumber=1 scan=20554

            .WriteStartElement("search_result")
        End With

        For Each oPSMEntry As PHRPReader.Data.PSM In lstHits

            mXMLWriter.WriteStartElement("search_hit")
            WriteAttribute("hit_rank", oPSMEntry.ScoreRank)

            Dim strPeptide As String = String.Empty
            Dim strCleanSequence As String
            Dim strPrefix As String = String.Empty
            Dim strSuffix As String = String.Empty

            If PeptideCleavageStateCalculator.SplitPrefixAndSuffixFromSequence(oPSMEntry.Peptide, strPeptide, strPrefix, strSuffix) Then
                ' The peptide sequence needs to be just the amino acids; no mod symbols
                strCleanSequence = PeptideCleavageStateCalculator.ExtractCleanSequenceFromSequenceWithMods(strPeptide, False)
                mXMLWriter.WriteAttributeString("peptide", strCleanSequence)
                mXMLWriter.WriteAttributeString("peptide_prev_aa", strPrefix)
                mXMLWriter.WriteAttributeString("peptide_next_aa", strSuffix)
            Else
                strCleanSequence = PeptideCleavageStateCalculator.ExtractCleanSequenceFromSequenceWithMods(oPSMEntry.Peptide, False)
                mXMLWriter.WriteAttributeString("peptide", strCleanSequence)
                mXMLWriter.WriteAttributeString("peptide_prev_aa", String.Empty)
                mXMLWriter.WriteAttributeString("peptide_next_aa", String.Empty)
            End If

            If strCleanSequence <> strPeptide Then
                mXMLWriter.WriteAttributeString("peptide_with_mods", oPSMEntry.PeptideWithNumericMods)
            End If

            mXMLWriter.WriteAttributeString("protein", oPSMEntry.ProteinFirst)

            ' Could optionally write out protein description
            '.WriteAttributeString("protein_descr", objSearchHit.StrProteinDescription)

            WriteAttribute("num_tot_proteins", oPSMEntry.Proteins.Count)
            WriteAttribute("num_matched_ions", 0)
            WriteAttribute("tot_num_ions", 0)
            WriteAttribute("calc_neutral_pep_mass", oPSMEntry.PeptideMonoisotopicMass, 4)

            If Not Double.TryParse(oPSMEntry.MassErrorDa, dblMassErrorDa) Then
                dblMassErrorDa = 0
            End If
            WriteAttributePlusMinus("massdiff", dblMassErrorDa, 5)

            ' Write the number of tryptic ends (0 for non-tryptic, 1 for partially tryptic, 2 for fully tryptic)
            WriteAttribute("num_tol_term", oPSMEntry.NumTrypticTermini)
            WriteAttribute("num_missed_cleavages", oPSMEntry.NumMissedCleavages)

            ' Initially all peptides will have "is_rejected" = 0
            WriteAttribute("is_rejected", 0)

            Dim lstProteins As List(Of Data.ProteinInfo) = Nothing
            Dim blnProteinInfoAvailable As Boolean
            Dim intNumTrypticTermini As Integer

            If Not lstSeqToProteinMap Is Nothing AndAlso lstSeqToProteinMap.Count > 0 Then
                blnProteinInfoAvailable = lstSeqToProteinMap.TryGetValue(oPSMEntry.SeqID, lstProteins)
            Else
                blnProteinInfoAvailable = False
            End If

            Dim intProteinsWritten = 0

            ' Write out the additional proteins
            For Each strProteinAddnl As String In oPSMEntry.Proteins

                If strProteinAddnl <> oPSMEntry.ProteinFirst Then
                    mXMLWriter.WriteStartElement("alternative_protein")
                    mXMLWriter.WriteAttributeString("protein", strProteinAddnl)
                    '.WriteAttributeString("protein_descr", strProteinAddnlDescription)

                    ' Initially use .NumTrypticTermini
                    ' We'll update this using lstProteins if possible
                    intNumTrypticTermini = oPSMEntry.NumTrypticTermini

                    If blnProteinInfoAvailable Then
                        For Each objProtein In lstProteins
                            If objProtein.ProteinName = strProteinAddnl Then
                                intNumTrypticTermini = CInt(objProtein.CleavageState)
                                Exit For
                            End If
                        Next
                    End If

                    WriteAttribute("num_tol_term", intNumTrypticTermini)

                    mXMLWriter.WriteEndElement()      ' alternative_protein

                End If

                intProteinsWritten += 1
                If MaxProteinsPerPSM > 0 AndAlso intProteinsWritten >= MaxProteinsPerPSM Then
                    Exit For
                End If
            Next


            If oPSMEntry.ModifiedResidues.Count > 0 Then
                mXMLWriter.WriteStartElement("modification_info")

                Dim dblNTermAddon As Double = 0
                Dim dblCTermAddon As Double = 0

                ' Look for N and C terminal mods in oPSMEntry.ModifiedResidues
                For Each objResidue In oPSMEntry.ModifiedResidues

                    If objResidue.ModDefinition.ModificationType = Data.ModificationDefinition.ResidueModificationType.TerminalPeptideStaticMod OrElse
                       objResidue.ModDefinition.ModificationType = Data.ModificationDefinition.ResidueModificationType.ProteinTerminusStaticMod Then

                        Select Case objResidue.TerminusState
                            Case Data.AminoAcidModInfo.ResidueTerminusState.PeptideNTerminus,
                             Data.AminoAcidModInfo.ResidueTerminusState.ProteinNTerminus,
                             Data.AminoAcidModInfo.ResidueTerminusState.ProteinNandCCTerminus
                                dblNTermAddon += objResidue.ModDefinition.ModificationMass

                            Case Data.AminoAcidModInfo.ResidueTerminusState.PeptideCTerminus,
                             Data.AminoAcidModInfo.ResidueTerminusState.ProteinCTerminus
                                dblCTermAddon += objResidue.ModDefinition.ModificationMass

                            Case Else
                                ' This is unexpected
                                OnErrorEvent("Peptide or Protein terminal mod found, but residue is not at a peptide or protein terminus: " &
                                             objResidue.Residue & " at position " & objResidue.ResidueLocInPeptide & " in peptide " &
                                             oPSMEntry.Peptide & ", scan " & oPSMEntry.ScanNumber)
                        End Select

                    End If

                Next

                ' If a peptide-terminal mod, add either of these attributes:
                If Math.Abs(dblNTermAddon) > Single.Epsilon Then
                    WriteAttributePlusMinus("mod_nterm_mass", (PeptideMassCalculator.DEFAULT_N_TERMINUS_MASS_CHANGE + dblNTermAddon), 5)
                End If

                If Math.Abs(dblCTermAddon) > Single.Epsilon Then
                    WriteAttributePlusMinus("mod_cterm_mass", (PeptideMassCalculator.DEFAULT_C_TERMINUS_MASS_CHANGE + dblCTermAddon), 5)
                End If


                ' Write out an entry for each modified amino acid
                ' We need to keep track of the total mass of each modified residue (excluding terminal mods) since a residue could have multiple modifications
                lstModifiedResidues.Clear()

                For Each objResidue In oPSMEntry.ModifiedResidues

                    If Not (objResidue.ModDefinition.ModificationType = Data.ModificationDefinition.ResidueModificationType.TerminalPeptideStaticMod OrElse
                      objResidue.ModDefinition.ModificationType = Data.ModificationDefinition.ResidueModificationType.ProteinTerminusStaticMod) Then

                        If lstModifiedResidues.TryGetValue(objResidue.ResidueLocInPeptide, dblTotalMass) Then
                            ' This residue has more than one modification applied to it
                            dblTotalMass += objResidue.ModDefinition.ModificationMass
                            lstModifiedResidues(objResidue.ResidueLocInPeptide) = dblTotalMass
                        Else
                            dblTotalMass = mPeptideMassCalculator.GetAminoAcidMass(objResidue.Residue) + objResidue.ModDefinition.ModificationMass
                            lstModifiedResidues.Add(objResidue.ResidueLocInPeptide, dblTotalMass)
                        End If

                    End If
                Next

                For Each objItem In lstModifiedResidues
                    mXMLWriter.WriteStartElement("mod_aminoacid_mass")

                    WriteAttribute("position", objItem.Key)     ' Position of residue in peptide
                    WriteAttribute("mass", objItem.Value, 5)    ' Total amino acid mass, including all mods (but excluding N or C terminal mods)

                    mXMLWriter.WriteEndElement()      ' mod_aminoacid_mass
                Next

                mXMLWriter.WriteEndElement()      ' modification_info
            End If

            ' Write out the search scores
            For Each objItem In oPSMEntry.AdditionalScores
                If mPNNLScoreNameMap.TryGetValue(objItem.Key, strAlternateScoreName) Then
                    WriteNameValueElement("search_score", strAlternateScoreName, objItem.Value)
                Else
                    WriteNameValueElement("search_score", objItem.Key, objItem.Value)
                End If
            Next


            WriteNameValueElement("search_score", "msgfspecprob", oPSMEntry.MSGFSpecEValue)

            ' Write out the mass error ppm value as a custom search score
            WriteNameValueElement("search_score", "MassErrorPPM", oPSMEntry.MassErrorPPM)

            If Not Double.TryParse(oPSMEntry.MassErrorPPM, dblMassErrorPPM) Then
                dblMassErrorPPM = 0
            End If

            WriteNameValueElement("search_score", "AbsMassErrorPPM", Math.Abs(dblMassErrorPPM), 4)

            '' Old, unused
            ' WritePeptideProphetUsingMSGF(mXMLWriter, objSearchHit, iNumTrypticTermini, iNumMissedCleavages)

            mXMLWriter.WriteEndElement()              ' search_hit
        Next

        mXMLWriter.WriteEndElement()            ' search_result

        mXMLWriter.WriteEndElement()            ' spectrum_query

    End Sub

    ' Old, unused
    'Private Sub WritePeptideProphetUsingMSGF(ByRef mXMLWriter As System.Xml.XmlWriter, ByRef objSearchHit As clsSearchHit, ByVal iNumTrypticTermini As Integer, ByVal iNumMissedCleavages As Integer)

    '	Dim strMSGF As String
    '	Dim strFVal As String

    '	With mXMLWriter
    '		.WriteStartElement("analysis_result")
    '		.WriteAttributeString("analysis", "peptideprophet")


    '		.WriteStartElement("peptideprophet_result")
    '		strMSGF = clsMSGFConversion.MSGFToProbability(objSearchHit.dMSGFSpecProb).ToString("0.0000")
    '		strFVal = clsMSGFConversion.MSGFToFValue(objSearchHit.dMSGFSpecProb).ToString("0.0000")

    '		.WriteAttributeString("probability", strMSGF)
    '		.WriteAttributeString("all_ntt_prob", "(" & strMSGF & "," & strMSGF & "," & strMSGF & ")")

    '		.WriteStartElement("search_score_summary")

    '		.WriteStartElement("parameter")
    '		.WriteAttributeString("name", "fval")
    '		.WriteAttributeString("value", strFVal)
    '		.WriteEndElement()

    '		.WriteStartElement("parameter")
    '		.WriteAttributeString("name", "ntt")
    '		.WriteAttributeString("value", iNumTrypticTermini.ToString("0"))
    '		.WriteEndElement()

    '		.WriteStartElement("parameter")
    '		.WriteAttributeString("name", "nmc")
    '		.WriteAttributeString("value", iNumMissedCleavages.ToString("0"))
    '		.WriteEndElement()

    '		.WriteStartElement("parameter")
    '		.WriteAttributeString("name", "massd")
    '		.WriteAttributeString("value", objSearchHit.dMassdiff.ToString("0.000"))
    '		.WriteEndElement()

    '		.WriteEndElement()			  ' search_score_summary

    '		.WriteEndElement()			  ' peptideprophet_result

    '		.WriteEndElement()			  ' analysis_result

    '	End With

    'End Sub

    ' Old, unused
    'Private Class clsMSGFConversion

    '	''' <summary>
    '	''' Performs a crude approximation of Probability using a MSGF SpecProb value
    '	''' Converts the MSGF score to base-10 log, adds 6, then converts back to the original scale to obtain an adjusted MSGF SpecProb value
    '	''' For example, if MSGF SpecProb = 1E-13, then the adjusted value is 1E-07
    '	''' Computes Probability as 1 - AdjustedMSGFSpecProb
    '	''' </summary>
    '	''' <param name="dblMSGFScore">MSGF SpecProb to convert</param>
    '	''' <returns>Probability</returns>
    '	Public Shared Function MSGFToProbability(dblMSGFScore As Double) As Double
    '		Const LOG_MSGF_ADJUST As Integer = 6

    '		Dim dLogMSGF As Double
    '		Dim dblProbability As Double

    '		If dblMSGFScore >= 1 Then
    '			dblProbability = 0
    '		ElseIf dblMSGFScore <= 0 Then
    '			dblProbability = 1
    '		Else
    '			dLogMSGF = Math.Log(dblMSGFScore, 10) + LOG_MSGF_ADJUST
    '			dblProbability = 1 - Math.Pow(10, dLogMSGF)

    '			If dblProbability < 0 Then
    '				dblProbability = 0
    '			End If

    '			If dblProbability > 1 Then
    '				dblProbability = 1
    '			End If
    '		End If

    '		Return dblProbability

    '	End Function

    '	''' <summary>
    '	''' Performs a crude approximation of FValue using a MSGF SpecProb value
    '	''' Converts the MSGF score to base-10 log, adds 6, then takes the negative of this result
    '	''' For example, if MSGF SpecProb = 1E-13, then computes: -13 + 6 = -7, then returns 7
    '	''' </summary>
    '	''' <param name="dblMSGFScore">MSGF SpecProb to convert</param>
    '	''' <returns>FValue</returns>
    '	Public Shared Function MSGFToFValue(dblMSGFScore As Double) As Double
    '		Const LOG_MSGF_ADJUST As Integer = 6

    '		Dim dLogMSGF As Double
    '		Dim dblFValue As Double

    '		If dblMSGFScore >= 1 Then
    '			dblFValue = 0
    '		Else
    '			dLogMSGF = Math.Log(dblMSGFScore, 10) + LOG_MSGF_ADJUST
    '			dblFValue = -dLogMSGF
    '		End If

    '		Return dblFValue

    '	End Function

    'End Class
End Class
