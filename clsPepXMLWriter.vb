Option Strict On

Imports PHRPReader

Public Class clsPepXMLWriter

#Region "Structures"

	Public Structure udtSpectrumInfoType
		Public SpectrumName As String			' Spectrum Title: could be "QC_05_2_05Dec05_Doc_0508-08.9427.9427.1" or just "scan=16134 cs=2"
		Public StartScan As Integer
		Public EndScan As Integer
		Public PrecursorNeutralMass As Double
		Public AssumedCharge As Integer
		Public ElutionTimeMinutes As Double
		Public Index As Integer
	End Structure

#End Region


#Region "Module-wide variables"
	Protected mCleavageStateCalculator As PHRPReader.clsPeptideCleavageStateCalculator

	Protected mXMLWriter As System.Xml.XmlWriter

	Protected mSearchEngineParams As clsSearchEngineParameters
	Protected mDatasetName As String
	Protected mFileOpen As Boolean

	' This dictionary maps PNNL-based score names to pep-xml standard score names (at present, only for Sequest)
	Protected mPNNLScoreNameMap As System.Collections.Generic.Dictionary(Of String, String)
#End Region

#Region "Properties"
	Public ReadOnly Property DatasetName As String
		Get
			Return mDatasetName
		End Get
	End Property

	Public ReadOnly Property IsWritable As Boolean
		Get
			Return mFileOpen
		End Get
	End Property

	Public ReadOnly Property SearchEngineParams As clsSearchEngineParameters
		Get
			Return mSearchEngineParams
		End Get
	End Property

#End Region

	Public Sub New(ByVal strDatasetName As String, ByVal strFastaFilePath As String, ByVal objSearchEngineParams As clsSearchEngineParameters, ByVal strOutputFilePath As String)

		mSearchEngineParams = objSearchEngineParams
		mDatasetName = strDatasetName
		If mDatasetName Is Nothing Then mDatasetName = "Unknown"

		mCleavageStateCalculator = New PHRPReader.clsPeptideCleavageStateCalculator()

		InitializePNNLScoreNameMap()

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

		mXMLWriter.WriteEndElement()				' msms_run_summary

		mXMLWriter.WriteEndElement()				' msms_pipeline_analysis
		mXMLWriter.WriteEndDocument()

		Return True
	End Function

	''' <summary>
	''' Initialize a Pep.XML file for writing
	''' </summary>
	''' <param name="strOutputFilePath"></param>
	''' <param name="strFastaFilePath"></param>
	''' <returns></returns>
	''' <remarks></remarks>
	Protected Function InitializePepXMLFile(ByVal strOutputFilePath As String, ByVal strFastaFilePath As String) As Boolean

		Dim fiOutputFile As System.IO.FileInfo

		If String.IsNullOrWhiteSpace(strFastaFilePath) Then
			strFastaFilePath = "C:\Database\ID_001260_20C38064.fasta"
		End If

		fiOutputFile = New System.IO.FileInfo(strOutputFilePath)

		Dim oSettings As System.Xml.XmlWriterSettings = New System.Xml.XmlWriterSettings()
		oSettings.Indent = True
		oSettings.OmitXmlDeclaration = False
		oSettings.NewLineOnAttributes = False
		oSettings.Encoding = System.Text.Encoding.ASCII

		mXMLWriter = System.Xml.XmlWriter.Create(strOutputFilePath, oSettings)
		mFileOpen = True

		mXMLWriter.WriteStartDocument()
		mXMLWriter.WriteProcessingInstruction("xml-stylesheet", "type=""text/xsl"" href=""pepXML_std.xsl""")

		WriteHeaderElements(fiOutputFile)

		WriteSearchSummary(strFastaFilePath)

		Return True

	End Function

	Protected Sub InitializePNNLScoreNameMap()
		mPNNLScoreNameMap = New System.Collections.Generic.Dictionary(Of String, String)(StringComparer.CurrentCultureIgnoreCase)

		mPNNLScoreNameMap.Add("XCorr", "xcorr")
		mPNNLScoreNameMap.Add("DelCn", "deltacn")
		mPNNLScoreNameMap.Add("Sp", "spscore")
		mPNNLScoreNameMap.Add("DelCn2", "deltacnstar")
		mPNNLScoreNameMap.Add("RankSp", "sprank")
	End Sub

	Protected Sub WriteAttribute(ByVal strAttributeName As String, ByVal Value As String)
		If String.IsNullOrEmpty(Value) Then
			mXMLWriter.WriteAttributeString(strAttributeName, String.Empty)
		Else
			mXMLWriter.WriteAttributeString(strAttributeName, Value)
		End If
	End Sub

	Protected Sub WriteAttribute(ByVal strAttributeName As String, ByVal Value As Integer)
		mXMLWriter.WriteAttributeString(strAttributeName, Value.ToString())
	End Sub

	Protected Sub WriteAttribute(ByVal strAttributeName As String, ByVal Value As Single)
		WriteAttribute(strAttributeName, Value, DigitsOfPrecision:=4)
	End Sub

	Protected Sub WriteAttribute(ByVal strAttributeName As String, ByVal Value As Single, ByVal DigitsOfPrecision As Integer)
		Dim strFormatString As String = "0"
		If DigitsOfPrecision > 0 Then
			strFormatString &= "." & New String("0"c, DigitsOfPrecision)
		End If

		mXMLWriter.WriteAttributeString(strAttributeName, Value.ToString(strFormatString))
	End Sub

	Protected Sub WriteAttributePlusMinus(ByVal strAttributeName As String, ByVal Value As Double, ByVal DigitsOfPrecision As Integer)
		Dim strFormatString As String = "+0;-0"
		If DigitsOfPrecision > 0 Then
			strFormatString = "+0." & New String("0"c, DigitsOfPrecision) & ";-0." & New String("0"c, DigitsOfPrecision)
		End If

		mXMLWriter.WriteAttributeString(strAttributeName, Value.ToString(strFormatString))
	End Sub

	Protected Sub WriteAttribute(ByVal strAttributeName As String, ByVal Value As Double)
		WriteAttribute(strAttributeName, Value, DigitsOfPrecision:=4)
	End Sub

	Protected Sub WriteAttribute(ByVal strAttributeName As String, ByVal Value As Double, ByVal DigitsOfPrecision As Integer)
		Dim strFormatString As String = "0"
		If DigitsOfPrecision > 0 Then
			strFormatString &= "." & New String("0"c, DigitsOfPrecision)
		End If

		mXMLWriter.WriteAttributeString(strAttributeName, Value.ToString(strFormatString))
	End Sub

	Protected Sub WriteNameValueElement(ByVal strElementName As String, ByVal strName As String, ByVal Value As String)
		mXMLWriter.WriteStartElement(strElementName)
		WriteAttribute("name", strName)
		WriteAttribute("value", Value)
		mXMLWriter.WriteEndElement()
	End Sub

	Protected Sub WriteNameValueElement(ByVal strElementName As String, ByVal strName As String, ByVal Value As Double)
		mXMLWriter.WriteStartElement(strElementName)
		WriteAttribute("name", strName)
		WriteAttribute("value", Value)
		mXMLWriter.WriteEndElement()
	End Sub

	Protected Sub WriteNameValueElement(ByVal strElementName As String, ByVal strName As String, ByVal Value As Double, ByVal DigitsOfPrecision As Integer)
		mXMLWriter.WriteStartElement(strElementName)
		WriteAttribute("name", strName)
		WriteAttribute("value", Value, DigitsOfPrecision)
		mXMLWriter.WriteEndElement()
	End Sub

	Protected Sub WriteHeaderElements(ByVal fiOutputFile As System.IO.FileInfo)

		With mXMLWriter

			.WriteStartElement("msms_pipeline_analysis", "http://regis-web.systemsbiology.net/pepXML")
			.WriteAttributeString("date", System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"))
			.WriteAttributeString("summary_xml", fiOutputFile.Name)
			.WriteAttributeString("xmlns", "http://regis-web.systemsbiology.net/pepXML")
			.WriteAttributeString("xmlns", "xsi", Nothing, "http://www.w3.org/2001/XMLSchema-instance")

			' Old:               ("xsi", "schemaLocation", Nothing, "http://regis-web.systemsbiology.net/pepXML c:\Inetpub\wwwrootpepXML_v113.xsd")
			.WriteAttributeString("xsi", "schemaLocation", Nothing, "http://sashimi.sourceforge.net/schema_revision/pepXML/pepXML_v117.xsd")

			.WriteStartElement("msms_run_summary")

			.WriteAttributeString("base_name", mDatasetName)
			.WriteAttributeString("raw_data_type", "raw")
			.WriteAttributeString("raw_data", ".mzXML")

			' ToDo: Determine enzyme info from SearchEngine info
			.WriteStartElement("sample_enzyme")
			.WriteAttributeString("name", "trypsin")

			.WriteStartElement("specificity")
			.WriteAttributeString("cut", "KR")
			.WriteAttributeString("no_cut", "P")
			.WriteAttributeString("sense", "C")
			.WriteEndElement()					' specificity

			.WriteEndElement()					' sample_enzyme

		End With

	End Sub

	Protected Sub WriteSearchSummary(ByVal strFastaFilePath As String)

		With mXMLWriter

			' ToDo: Parse the search engine parameter file to populate mSearchEngineParams

			.WriteStartElement("search_summary")

			.WriteAttributeString("base_name", mDatasetName & ".txt")		  ' Input file path

			.WriteAttributeString("search_engine", mSearchEngineParams.SearchEngineName)
			.WriteAttributeString("precursor_mass_type", mSearchEngineParams.PrecursorMassType)
			.WriteAttributeString("fragment_mass_type", mSearchEngineParams.FragmentMassType)

			.WriteAttributeString("search_id", "1")

			.WriteStartElement("search_database")
			.WriteAttributeString("local_path", strFastaFilePath)
			.WriteAttributeString("type", "AA")
			.WriteEndElement()		' search_database			
		End With

		mXMLWriter.WriteStartElement("enzymatic_search_constraint")
		WriteAttribute("enzyme", mSearchEngineParams.Enzyme)
		WriteAttribute("max_num_internal_cleavages", mSearchEngineParams.MaxNumberInternalCleavages)
		WriteAttribute("min_number_termini", mSearchEngineParams.MinNumberTermini)
		mXMLWriter.WriteEndElement()		' enzymatic_search_constraint

		' ToDo: Code up the mods

		' Amino acid mod details

		For Each objModDef As clsModificationDefinition In mSearchEngineParams.ModInfo
			If objModDef.CanAffectPeptideResidues() Then
				mXMLWriter.WriteStartElement("aminoacid_modification")

				' ToDo: Code this

				'WriteAttribute("aminoacid", udtModInfo.AminoAcid)							' Amino acid symbol, e.g. A
				'WriteAttributePlusMinus("massdiff", udtModInfo.ModMass, 5)			' Mass difference, must begin with + or -
				'WriteAttribute("mass", dblAAMass + udtModInfo.ModMass, 4)

				'If udtModInfo.Variable Then
				'	WriteAttribute("variable", "Y")
				'Else
				'	WriteAttribute("variable", "N")
				'End If
				'WriteAttribute("symbol", udtModInfo.Symbol)				' Symbol used by search-engine to denote this mod

				'WriteAttribute("description", udtModInfo.Description)

				mXMLWriter.WriteEndElement()		' aminoacid_modification
			End If
		Next

		' N/C terminal mods
		For Each objModDef As clsModificationDefinition In mSearchEngineParams.ModInfo
			If objModDef.CanAffectPeptideOrProteinTerminus() Then
				mXMLWriter.WriteStartElement("terminal_modification")

				' ToDo: Code this
				'If udtModInfo.Type = eNTerminal Then
				'	WriteAttribute("terminus", "n")
				'Else
				'	WriteAttribute("terminus", "c")
				'End If

				'WriteAttributePlusMinus("massdiff", udtModInfo.ModMass, 5)			' Mass difference, must beging with + or -
				'WriteAttribute("mass", dblAAMass + udtModInfo.ModMass, 4)

				'If udtModInfo.Variable Then
				'	WriteAttribute("variable", "Y")
				'Else
				'	WriteAttribute("variable", "N")
				'End If
				'WriteAttribute("symbol", udtModInfo.Symbol)				' Symbol used by search-engine to denote this mod

				'' XSD description at http://sashimi.sourceforge.net/schema_revision/pepXML/pepXML_v117.xsd
				'' has this for the "protein_terminus" attribute:  whether modification can reside only at protein terminus (specified n or c)
				'' This probably is a Y or N parameter, so for safety we'll always just use "N" (since that's what most examples have too)
				'WriteAttribute("protein_terminus", "N")

				'WriteAttribute("description", udtModInfo.Description)

				mXMLWriter.WriteEndElement()		' terminal_modification
			End If
		Next


		If mSearchEngineParams.Parameters Is Nothing OrElse mSearchEngineParams.Parameters.Count = 0 Then
			' Write out two dummy-parameters
			mXMLWriter.WriteComment("Dummy search-engine parameters")

			WriteNameValueElement("parameter", "peptide_mass_tol", "3.000")
			WriteNameValueElement("parameter", "fragment_ion_tol", "0.000")
		Else
			mXMLWriter.WriteComment("Search-engine parameters")
			' Write out the search-engine parameters
			For Each objItem As Generic.KeyValuePair(Of String, String) In mSearchEngineParams.Parameters
				WriteNameValueElement("parameter", objItem.Key, objItem.Value)
			Next
		End If

		mXMLWriter.WriteEndElement()					' search_summary

	End Sub

	Public Sub WriteSpectrum(ByRef objSpectrum As udtSpectrumInfoType, ByRef lstHits As System.Collections.Generic.List(Of clsPSM))

		Dim dblMassErrorDa As Double
		Dim strAlternateScoreName As String = String.Empty

		If lstHits Is Nothing OrElse lstHits.Count = 0 Then Exit Sub

		With mXMLWriter

			.WriteStartElement("spectrum_query")
			.WriteAttributeString("spectrum", objSpectrum.SpectrumName)			' Example: QC_05_2_05Dec05_Doc_0508-08.9427.9427.1
			WriteAttribute("start_scan", objSpectrum.StartScan)
			WriteAttribute("end_scan", objSpectrum.EndScan)

			WriteAttribute("precursor_neutral_mass", objSpectrum.PrecursorNeutralMass)

			WriteAttribute("assumed_charge", objSpectrum.AssumedCharge)
			WriteAttribute("index", objSpectrum.Index)

			.WriteStartElement("search_result")
		End With

		Dim intHitRank As Integer = 0
		For Each oPSMEntry As clsPSM In lstHits

			intHitRank += 1
			With mXMLWriter
				.WriteStartElement("search_hit")
				WriteAttribute("hit_rank", intHitRank)

				Dim strCleanSeq As String = ""
				Dim strPrefix As String = ""
				Dim strSuffix As String = ""

				If PHRPReader.clsPeptideCleavageStateCalculator.SplitPrefixAndSuffixFromSequence(oPSMEntry.Peptide, strCleanSeq, strPrefix, strSuffix) Then
					' The peptide sequence needs to be just the amino acids; no mod symbols
					.WriteAttributeString("peptide", PHRPReader.clsPeptideCleavageStateCalculator.ExtractCleanSequenceFromSequenceWithMods(strCleanSeq, False))
					.WriteAttributeString("peptide_prev_aa", strPrefix)
					.WriteAttributeString("peptide_next_aa", strSuffix)
				Else
					.WriteAttributeString("peptide", PHRPReader.clsPeptideCleavageStateCalculator.ExtractCleanSequenceFromSequenceWithMods(oPSMEntry.Peptide, False))
					.WriteAttributeString("peptide_prev_aa", String.Empty)
					.WriteAttributeString("peptide_next_aa", String.Empty)
				End If

				'Dim eCleavageState As PHRPReader.clsPeptideCleavageStateCalculator.ePeptideCleavageStateConstants
				'Dim iNumTrypticTerminii As Integer
				'eCleavageState = oPSMEntry.CleavageState
				'If eCleavageState = PHRPReader.clsPeptideCleavageStateCalculator.ePeptideCleavageStateConstants.Full Then
				'	iNumTrypticTerminii = 2
				'ElseIf eCleavageState = PHRPReader.clsPeptideCleavageStateCalculator.ePeptideCleavageStateConstants.Partial Then
				'	iNumTrypticTerminii = 1
				'Else
				'	iNumTrypticTerminii = 0
				'End If

				.WriteAttributeString("protein", oPSMEntry.ProteinFirst)

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
				WriteAttribute("num_tol_term", oPSMEntry.NumTrypticTerminii)
				WriteAttribute("num_missed_cleavages", oPSMEntry.NumMissedCleavages)

				' Initially all peptides will have "is_rejected" = 0
				WriteAttribute("is_rejected", 0)

				' Write out the additional proteins
				For Each strProteinAddnl As String In oPSMEntry.Proteins

					If strProteinAddnl <> oPSMEntry.ProteinFirst Then
						.WriteStartElement("alternative_protein")
						.WriteAttributeString("protein", strProteinAddnl)
						'.WriteAttributeString("protein_descr", strProteinAddnlDescription)

						' ToDo: Recompute the number of tryptic ends with relation to this specific protein
						' For now, just use objSearchHit.iNum_tol_term
						WriteAttribute("num_tol_term", oPSMEntry.NumTrypticTerminii)

						.WriteEndElement()		' alternative_protein

					End If
				Next

				' ToDo: Write out the modification info
				'If objSearchHit.AAMods.count > 0 Then
				'	.WriteStartElement("modification_info")

				'	For Each udtAAMod In objSearchHit.AAMods
				'		Dim dblNTermAddon As Double = 0
				'		Dim dblCTermAddon As Double = 0

				'		If udtAAMod.Type = eNTerminal Then
				'			dblNTermAddon += udtAAMod.ModMass
				'		End If

				'		If udtAAMod.Type = eCTerminal Then
				'			dblCTermAddon += udtAAMod.ModMass
				'		End If

				'		' If a peptide-terminal mod, add either of these attributes:
				'		If dblNTermAddon <> 0 Then
				'			WriteAttributePlusMinus("mod_nterm_mass", (dblMassH + dblNTermAddon), 5)
				'		End If

				'		If dblCTermAddon <> 0 Then
				'			WriteAttributePlusMinus("mod_cterm_mass", (dblMassOH + dblCTermAddon), 5)
				'		End If

				'	Next

				'	' Write out an entry for each modified amino acid
				'	For Each udtAAMod In objSearchHit.AAMods

				'		If Not (udtAAMod.Type = eNTerminal Or udtAAMod.Type = eCTerminal) Then
				'			.WriteStartElement("mod_aminoacid_mass")

				'			.WriteAttributeString("position", udtAAMod.TotalMassWithMod)		' Note that TotalMassWithMod needs to exclude N or C terminal mods
				'			.WriteAttributeString("mass", udtAAMod.TotalMassWithMod)

				'			.WriteEndElement()		' mod_aminoacid_mass
				'		End If
				'	Next

				'	.WriteEndElement()		' modification_info
				'End If

				' Write out the search scores
				For Each objItem In oPSMEntry.AdditionalScores
					If mPNNLScoreNameMap.TryGetValue(objItem.Key, strAlternateScoreName) Then
						WriteNameValueElement("search_score", strAlternateScoreName, objItem.Value)
					Else
						WriteNameValueElement("search_score", objItem.Key, objItem.Value)
					End If
				Next

			End With


			With mXMLWriter

				WriteNameValueElement("search_score", "msgfspecprob", oPSMEntry.MSGFSpecProb)

				Dim dblMSGFSpecProb As Double
				If Double.TryParse(oPSMEntry.MSGFSpecProb, dblMSGFSpecProb) Then
					WriteNameValueElement("search_score", "msgfspecprobAltFormat", dblMSGFSpecProb, 11)
					WriteNameValueElement("search_score", "msgfprobability", clsMSGFConversion.MSGFToProbability(dblMSGFSpecProb), 4)
					WriteNameValueElement("search_score", "msgffscore", clsMSGFConversion.MSGFToFValue(dblMSGFSpecProb), 4)
				End If

			End With

			'' Old, unused
			' WritePeptideProphetUsingMSGF(mXMLWriter, objSearchHit, iNumTrypticTerminii, iNumMissedCleavages)

			mXMLWriter.WriteEndElement()			  ' search_hit
		Next

		mXMLWriter.WriteEndElement()			' search_result

		mXMLWriter.WriteEndElement()			' spectrum_query

	End Sub

	' Old, unused
	'Protected Sub WritePeptideProphetUsingMSGF(ByRef mXMLWriter As System.Xml.XmlWriter, ByRef objSearchHit As clsSearchHit, ByVal iNumTrypticTerminii As Integer, ByVal iNumMissedCleavages As Integer)

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
	'		.WriteAttributeString("value", iNumTrypticTerminii.ToString("0"))
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

	Protected Class clsMSGFConversion

		''' <summary>
		''' Performs a crude approximation of Probability using a MSGF SpecProb value
		''' Converts the MSGF score to base-10 log, adds 6, then converts back to the original scale to obtain an adjusted MSGF SpecProb value
		''' For example, if MSGF SpecProb = 1E-13, then the adjusted value is 1E-07
		''' Computes Probability as 1 - AdjustedMSGFSpecProb
		''' </summary>
		''' <param name="dblMSGFScore">MSGF SpecProb to convert</param>
		''' <returns>Probability</returns>
		Public Shared Function MSGFToProbability(dblMSGFScore As Double) As Double
			Const LOG_MSGF_ADJUST As Integer = 6

			Dim dLogMSGF As Double
			Dim dblProbability As Double

			If dblMSGFScore >= 1 Then
				dblProbability = 0
			ElseIf dblMSGFScore <= 0 Then
				dblProbability = 1
			Else
				dLogMSGF = Math.Log(dblMSGFScore, 10) + LOG_MSGF_ADJUST
				dblProbability = 1 - Math.Pow(10, dLogMSGF)

				If dblProbability < 0 Then
					dblProbability = 0
				End If

				If dblProbability > 1 Then
					dblProbability = 1
				End If
			End If

			Return dblProbability

		End Function

		''' <summary>
		''' Performs a crude approximation of FValue using a MSGF SpecProb value
		''' Converts the MSGF score to base-10 log, adds 6, then takes the negative of this result
		''' For example, if MSGF SpecProb = 1E-13, then computes: -13 + 6 = -7, then returns 7
		''' </summary>
		''' <param name="dblMSGFScore">MSGF SpecProb to convert</param>
		''' <returns>FValue</returns>
		Public Shared Function MSGFToFValue(dblMSGFScore As Double) As Double
			Const LOG_MSGF_ADJUST As Integer = 6

			Dim dLogMSGF As Double
			Dim dblFValue As Double

			If dblMSGFScore >= 1 Then
				dblFValue = 0
			Else
				dLogMSGF = Math.Log(dblMSGFScore, 10) + LOG_MSGF_ADJUST
				dblFValue = -dLogMSGF
			End If

			Return dblFValue

		End Function

	End Class
End Class
