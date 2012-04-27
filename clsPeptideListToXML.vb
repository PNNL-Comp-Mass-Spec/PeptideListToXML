Option Strict On

' This class will read a tab-delimited text file with peptides and scores
' and create a new PepXML or mzIdentML file with the peptides
'
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
'
' Started April 13, 2012

Public Class clsPeptideListToXML
    Inherits clsProcessFilesBaseClass

    Public Sub New()
		MyBase.mFileDate = "April 27, 2012"
        InitializeLocalVariables()
    End Sub

#Region "Constants and Enums"

    Public Const XML_SECTION_OPTIONS As String = "PeptideListToXMLOptions"
	Public Const DEFAULT_HITS_PER_SPECTRUM As Integer = 3

	' Future enum; mzIdentML is not yet supported
	'Public Enum ePeptideListOutputFormat
	'    PepXML = 0
	'    mzIdentML = 1
	'End Enum

	''' <summary>
	''' Error codes specialized for this class
	''' </summary>
	''' <remarks></remarks>
    Public Enum ePeptideListToXMLErrorCodes
        NoError = 0
        ErrorReadingInputFile = 1
		ErrorWritingOutputFile = 2
		ModSummaryFileNotFound = 3
		SeqInfoFileNotFound = 4
		MSGFStatsFileNotFound = 5
		ScanStatsFileNotFound = 6
        UnspecifiedError = -1
    End Enum

#End Region

#Region "Structures"

#End Region

#Region "Classwide Variables"

	' Future enum; mzIdentML is not yet supported
	'Protected mOutputFormat As clsPeptideListToXML.ePeptideListOutputFormat

	Protected WithEvents mPHRPReader As PHRPReader.clsPHRPReader
	Protected WithEvents mXMLWriter As clsPepXMLWriter

	' Note that DatasetName is auto-determined via ConvertPHRPDataToXML()
	Protected mDatasetName As String
	Protected mPeptideHitResultType As PHRPReader.clsPHRPReader.ePeptideHitResultType
	Protected mSeqToProteinMapCached As System.Collections.Generic.SortedList(Of Integer, System.Collections.Generic.List(Of PHRPReader.clsProteinInfo))

	' Note that FastaFilePath will be ignored if the Search Engine Param File exists and it contains a fasta file name
	Protected mFastaFilePath As String
	Protected mSearchEngineParamFileName As String
	Protected mHitsPerSpectrum As Integer				 ' Number of hits per spectrum to store; 0 means to store all hits
	Protected mPreviewMode As Boolean
	Protected mSkipXPeptides As Boolean

	Protected mLoadModsAndSeqInfo As Boolean
	Protected mLoadMSGFResults As Boolean
	Protected mLoadScanStats As Boolean

	' This dictionary tracks the PSMs (hits) for each spectrum
	' The key is the Spectrum Key string (dataset, start scan, end scan, charge)
	Protected mPSMsBySpectrumKey As System.Collections.Generic.Dictionary(Of String, System.Collections.Generic.List(Of PHRPReader.clsPSM))

	' This dictionary tracks the spectrum info
	' The key is the Spectrum Key string (dataset, start scan, end scan, charge)
	Protected mSpectrumInfo As System.Collections.Generic.Dictionary(Of String, clsPepXMLWriter.udtSpectrumInfoType)

	Private mLocalErrorCode As ePeptideListToXMLErrorCodes
#End Region

#Region "Processing Options Interface Functions"

	''' <summary>
	''' Dataset name; auto-determined by the PHRP Reader class
	''' </summary>
	''' <value></value>
	''' <returns></returns>
	''' <remarks></remarks>
	Public ReadOnly Property DatasetName() As String
		Get
			Return mDatasetName
		End Get
	End Property

	''' <summary>
	''' Fasta file path to store in the pepXML file
	''' Ignored if the Search Engine Param File exists and it contains a fasta file name (typically the case for Sequest and X!Tandem)
	''' </summary>
	''' <value></value>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Property FastaFilePath() As String
		Get
			Return mFastaFilePath
		End Get
		Set(value As String)
			mFastaFilePath = value
		End Set
	End Property

	''' <summary>
	''' Number of peptides per spectrum to store in the PepXML file; 0 means store all hits
	''' </summary>
	''' <value></value>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Property HitsPerSpectrum() As Integer
		Get
			Return mHitsPerSpectrum
		End Get
		Set(value As Integer)
			mHitsPerSpectrum = value
		End Set
	End Property

	Public Property LoadModsAndSeqInfo() As Boolean
		Get
			Return mLoadModsAndSeqInfo
		End Get
		Set(value As Boolean)
			mLoadModsAndSeqInfo = value
		End Set
	End Property

	Public Property LoadMSGFResults() As Boolean
		Get
			Return mLoadMSGFResults
		End Get
		Set(value As Boolean)
			mLoadMSGFResults = value
		End Set
	End Property

	Public Property LoadScanStats() As Boolean
		Get
			Return mLoadScanStats
		End Get
		Set(value As Boolean)
			mLoadScanStats = value
		End Set
	End Property

	''' <summary>
	''' Local error code
	''' </summary>
	''' <value></value>
	''' <returns></returns>
	''' <remarks></remarks>
	Public ReadOnly Property LocalErrorCode() As ePeptideListToXMLErrorCodes
		Get
			Return mLocalErrorCode
		End Get
	End Property

	''' <summary>
	''' If true, then displays the names of the files that are required to create the PepXML file for the specified dataset
	''' </summary>
	''' <value></value>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Property PreviewMode As Boolean
		Get
			Return mPreviewMode
		End Get
		Set(value As Boolean)
			mPreviewMode = value
		End Set
	End Property

	''' <summary>
	''' Name of the paramter file used by the search engine that produced the results file that we are parsing
	''' </summary>
	''' <value></value>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Property SearchEngineParamFileName() As String
		Get
			Return mSearchEngineParamFileName
		End Get
		Set(value As String)
			mSearchEngineParamFileName = value
		End Set
	End Property

	''' <summary>
	''' If True, then skip peptides with X residues
	''' </summary>
	''' <value></value>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Property SkipXPeptides As Boolean
		Get
			Return mSkipXPeptides
		End Get
		Set(value As Boolean)
			mSkipXPeptides = value
		End Set
	End Property

	' Future enum; mzIdentML is not yet supported
	'Public Property OutputFormat() As ePeptideListOutputFormat
	'    Get
	'        Return mOutputFormat
	'    End Get
	'    Set(value As ePeptideListOutputFormat)
	'        mOutputFormat = value
	'    End Set
	'End Property

#End Region

	''' <summary>
	''' Create a PepXML file using the peptides in file strInputFilePath
	''' </summary>
	''' <param name="strInputFilePath"></param>
	''' <param name="strOutputFolderPath"></param>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Function ConvertPHRPDataToXML(ByVal strInputFilePath As String, ByVal strOutputFolderPath As String) As Boolean

		Dim objSearchEngineParams As PHRPReader.clsSearchEngineParameters = Nothing
		Dim strOutputFilePath As String
		Dim blnSuccess As Boolean

		' Note that CachePHRPData() will update these variables
		mDatasetName = "Unknown"
		mPeptideHitResultType = PHRPReader.clsPHRPReader.ePeptideHitResultType.Unknown

		blnSuccess = CachePHRPData(strInputFilePath, objSearchEngineParams)

		If Not blnSuccess Then Return False

		If mPreviewMode Then
			PreviewRequiredFiles(strInputFilePath, mDatasetName, mPeptideHitResultType, mLoadModsAndSeqInfo, mLoadMSGFResults, mLoadScanStats, mSearchEngineParamFileName)
		Else
			strOutputFilePath = System.IO.Path.Combine(strOutputFolderPath, mDatasetName & ".pepXML")
			blnSuccess = WriteCachedData(strInputFilePath, strOutputFilePath, objSearchEngineParams)
		End If

		Return blnSuccess

	End Function

	Protected Function CachePHRPData(ByVal strInputFilePath As String, ByRef objSearchEngineParams As PHRPReader.clsSearchEngineParameters) As Boolean

		Dim blnSuccess As Boolean
		Dim strSpectrumKey As String
		Dim udtSpectrumInfo As clsPepXMLWriter.udtSpectrumInfoType
		Dim objPSMs As System.Collections.Generic.List(Of PHRPReader.clsPSM) = Nothing
		Dim blnSkipPeptide As Boolean

		Dim intPeptidesParsed As Integer
		Dim intPeptidesStored As Integer

		Try
			If mPreviewMode Then
				ShowMessage("Finding required files")
			Else
				ShowMessage("Caching PHRP data")
			End If

			If mPSMsBySpectrumKey Is Nothing Then
				mPSMsBySpectrumKey = New System.Collections.Generic.Dictionary(Of String, System.Collections.Generic.List(Of PHRPReader.clsPSM))
			Else
				mPSMsBySpectrumKey.Clear()
			End If

			If mSpectrumInfo Is Nothing Then
				mSpectrumInfo = New System.Collections.Generic.Dictionary(Of String, clsPepXMLWriter.udtSpectrumInfoType)
			Else
				mSpectrumInfo.Clear()
			End If

			intPeptidesParsed = 0
			intPeptidesStored = 0

			mPHRPReader = New PHRPReader.clsPHRPReader(strInputFilePath, mLoadModsAndSeqInfo, mLoadMSGFResults, mLoadScanStats)

			mDatasetName = mPHRPReader.DatasetName
			mPeptideHitResultType = mPHRPReader.PeptideHitResultType
			mSeqToProteinMapCached = mPHRPReader.PHRPParser.SeqToProteinMap

			If mPreviewMode Then
				' We can exit this function now since we have determined the dataset name and peptide hit result type
				Return True
			End If

			' Report any errors cached during instantiation of mPHRPReader
			For Each strMessage In mPHRPReader.ErrorMessages
				ShowErrorMessage(strMessage)
			Next

			' Report any warnings cached during instantiation of mPHRPReader
			For Each strMessage In mPHRPReader.WarningMessages
				Console.WriteLine()
				ShowWarningMessage(strMessage)

				If strMessage.Contains("SeqInfo file not found") Then
					If mPHRPReader.ModSummaryFileLoaded Then
						ShowMessage("  ... will use the ModSummary file to infer the peptide modifications")
					Else
						ShowMessage("  ... use the /NoMods switch to avoid this error (though modified peptides in that case modified peptides would not be stored properly)")
					End If

				ElseIf strMessage.Contains("MSGF file not found") Then
					ShowMessage("  ... use the /NoMSGF switch to avoid this error")
				ElseIf strMessage.Contains("Extended ScanStats file not found") Then
					ShowMessage("  ... parent ion m/z values may not be completely accurate; use the /NoScanStats switch to avoid this error")
				ElseIf strMessage.Contains("ScanStats file not found") Then
					ShowMessage("  ... use the /NoScanStats switch to avoid this error")
				End If
			Next
			If mPHRPReader.WarningMessages.Count > 0 Then Console.WriteLine()

			mPHRPReader.ClearErrors()
			mPHRPReader.ClearWarnings()

			If String.IsNullOrEmpty(mDatasetName) Then
				mDatasetName = "Unknown"
				ShowWarningMessage("Unable to determine the dataset name from the input file path; database will be named " & mDatasetName & " in the PepXML file")
			End If

			While mPHRPReader.MoveNext

				Dim objCurrentPSM As PHRPReader.clsPSM = mPHRPReader.CurrentPSM

				If mSkipXPeptides AndAlso objCurrentPSM.PeptideCleanSequence.Contains("X"c) Then
					blnSkipPeptide = True
				Else
					blnSkipPeptide = False
				End If

				If Not blnSkipPeptide AndAlso mHitsPerSpectrum > 0 Then
					If objCurrentPSM.ScoreRank > mHitsPerSpectrum Then
						blnSkipPeptide = True
					End If
				End If

				If Not blnSkipPeptide Then

					strSpectrumKey = GetSpectrumKey(objCurrentPSM)

					If Not mSpectrumInfo.ContainsKey(strSpectrumKey) Then
						' New spectrum; add a new entry to mSpectrumInfo
						udtSpectrumInfo = New clsPepXMLWriter.udtSpectrumInfoType
						udtSpectrumInfo.SpectrumName = strSpectrumKey
						udtSpectrumInfo.StartScan = objCurrentPSM.ScanNumberStart
						udtSpectrumInfo.EndScan = objCurrentPSM.ScanNumberEnd
						udtSpectrumInfo.PrecursorNeutralMass = objCurrentPSM.PrecursorNeutralMass
						udtSpectrumInfo.AssumedCharge = objCurrentPSM.Charge
						udtSpectrumInfo.ElutionTimeMinutes = objCurrentPSM.ElutionTimeMinutes
						udtSpectrumInfo.CollisionMode = objCurrentPSM.CollisionMode
						udtSpectrumInfo.Index = mSpectrumInfo.Count

						mSpectrumInfo.Add(strSpectrumKey, udtSpectrumInfo)
					End If

					If mPSMsBySpectrumKey.TryGetValue(strSpectrumKey, objPSMs) Then
						objPSMs.Add(objCurrentPSM)
					Else
						objPSMs = New System.Collections.Generic.List(Of PHRPReader.clsPSM)
						objPSMs.Add(objCurrentPSM)
						mPSMsBySpectrumKey.Add(strSpectrumKey, objPSMs)
					End If

					intPeptidesStored += 1

				End If

				intPeptidesParsed += 1

				UpdateProgress(mPHRPReader.PercentComplete)

			End While

			OperationComplete()
			Console.WriteLine()
			ShowMessage(" ... cached " & intPeptidesStored.ToString("0,000") & " peptides")

			' Load the search engine parameters
			objSearchEngineParams = LoadSearchEngineParameters(mPHRPReader, mSearchEngineParamFileName)

			blnSuccess = True

		Catch ex As Exception
			If mPreviewMode Then
				Console.WriteLine()
				ShowMessage("Unable to preview the required files since not able to determine the dataset name: " & ex.Message)
			Else
				ShowErrorMessage("Error Reading source file in CachePHRPData: " & ex.Message)

				If ex.Message.Contains("ModSummary file not found") Then
					SetLocalErrorCode(ePeptideListToXMLErrorCodes.ModSummaryFileNotFound)
				Else
					SetLocalErrorCode(ePeptideListToXMLErrorCodes.ErrorReadingInputFile)
				End If
			End If

			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	''' <summary>
	''' Returns the default file extensions that this class knows how to parse
	''' </summary>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Overrides Function GetDefaultExtensionsToParse() As String()
		Dim strExtensionsToParse(3) As String

		strExtensionsToParse(0) = PHRPReader.clsPHRPParserSequest.GetPHRPSynopsisFileName("")
		strExtensionsToParse(1) = PHRPReader.clsPHRPParserXTandem.GetPHRPSynopsisFileName("")
		strExtensionsToParse(2) = PHRPReader.clsPHRPParserMSGFDB.GetPHRPSynopsisFileName("")
		strExtensionsToParse(3) = PHRPReader.clsPHRPParserInspect.GetPHRPSynopsisFileName("")

		Return strExtensionsToParse

	End Function

	''' <summary>
	''' Returns the error message; empty string if no error
	''' </summary>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Overrides Function GetErrorMessage() As String

		Dim strErrorMessage As String

		If MyBase.ErrorCode = clsProcessFilesBaseClass.eProcessFilesErrorCodes.LocalizedError Or _
		   MyBase.ErrorCode = clsProcessFilesBaseClass.eProcessFilesErrorCodes.NoError Then
			Select Case mLocalErrorCode
				Case ePeptideListToXMLErrorCodes.NoError
					strErrorMessage = ""

				Case ePeptideListToXMLErrorCodes.ErrorReadingInputFile
					strErrorMessage = "Error reading input file"

				Case ePeptideListToXMLErrorCodes.ErrorWritingOutputFile
					strErrorMessage = "Error writing to the output file"

				Case ePeptideListToXMLErrorCodes.ModSummaryFileNotFound
					strErrorMessage = "ModSummary file not found; use the /NoMods switch to avoid this error (though modified peptides will not be stored properly)"

				Case ePeptideListToXMLErrorCodes.SeqInfoFileNotFound
					strErrorMessage = "SeqInfo file not found; use the /NoMods switch to avoid this error (though modified peptides will not be stored properly)"

				Case ePeptideListToXMLErrorCodes.MSGFStatsFileNotFound
					strErrorMessage = "MSGF file not found; use the /NoMSGF switch to avoid this error"

				Case ePeptideListToXMLErrorCodes.ScanStatsFileNotFound
					strErrorMessage = "MASIC ScanStats file not found; use the /NoScanStats switch to avoid this error"

				Case ePeptideListToXMLErrorCodes.UnspecifiedError
					strErrorMessage = "Unspecified localized error"
				Case Else
					' This shouldn't happen
					strErrorMessage = "Unknown error state"
			End Select
		Else
			strErrorMessage = MyBase.GetBaseClassErrorMessage()
		End If

		Return strErrorMessage

	End Function

	Protected Function GetSpectrumKey(ByRef CurrentPSM As PHRPReader.clsPSM) As String
		Return mDatasetName & "." & CurrentPSM.ScanNumberStart & "." & CurrentPSM.ScanNumberEnd & "." & CurrentPSM.Charge
	End Function

	Private Sub InitializeLocalVariables()
		mLocalErrorCode = ePeptideListToXMLErrorCodes.NoError

		mDatasetName = "Unknown"
		mPeptideHitResultType = PHRPReader.clsPHRPReader.ePeptideHitResultType.Unknown

		mFastaFilePath = String.Empty
		mSearchEngineParamFileName = String.Empty

		mHitsPerSpectrum = DEFAULT_HITS_PER_SPECTRUM
		mSkipXPeptides = False

		mLoadModsAndSeqInfo = True
		mLoadMSGFResults = True
		mLoadScanStats = True

	End Sub

	''' <summary>
	''' Loads the settings from the parameter file
	''' </summary>
	''' <param name="strParameterFilePath"></param>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Function LoadParameterFileSettings(ByVal strParameterFilePath As String) As Boolean

		Dim objSettingsFile As New XmlSettingsFileAccessor

		Try

			If strParameterFilePath Is Nothing OrElse strParameterFilePath.Length = 0 Then
				' No parameter file specified; nothing to load
				Return True
			End If

			If Not System.IO.File.Exists(strParameterFilePath) Then
				' See if strParameterFilePath points to a file in the same directory as the application
				strParameterFilePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), System.IO.Path.GetFileName(strParameterFilePath))
				If Not System.IO.File.Exists(strParameterFilePath) Then
					MyBase.SetBaseClassErrorCode(clsProcessFilesBaseClass.eProcessFilesErrorCodes.ParameterFileNotFound)
					Return False
				End If
			End If

			If objSettingsFile.LoadSettings(strParameterFilePath) Then
				If Not objSettingsFile.SectionPresent(XML_SECTION_OPTIONS) Then
					ShowErrorMessage("The node '<section name=""" & XML_SECTION_OPTIONS & """> was not found in the parameter file: " & strParameterFilePath)
					MyBase.SetBaseClassErrorCode(clsProcessFilesBaseClass.eProcessFilesErrorCodes.InvalidParameterFile)
					Return False
				Else

					Me.FastaFilePath = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "FastaFilePath", Me.FastaFilePath)
					Me.SearchEngineParamFileName = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "SearchEngineParamFileName", Me.SearchEngineParamFileName)
					Me.HitsPerSpectrum = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "HitsPerSpectrum", Me.HitsPerSpectrum)
					Me.SkipXPeptides = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "SkipXPeptides", Me.SkipXPeptides)

					Me.LoadModsAndSeqInfo = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "LoadModsAndSeqInfo", Me.LoadModsAndSeqInfo)
					Me.LoadMSGFResults = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "LoadMSGFResults", Me.LoadMSGFResults)
					Me.LoadScanStats = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "LoadScanStats", Me.LoadScanStats)

				End If
			End If

		Catch ex As Exception
			HandleException("Error in LoadParameterFileSettings", ex)
			Return False
		End Try

		Return True

	End Function

	Protected Function LoadSearchEngineParameters(ByRef objPHRPReader As PHRPReader.clsPHRPReader, ByVal strSearchEngineParamFileName As String) As PHRPReader.clsSearchEngineParameters
		Dim objSearchEngineParams As PHRPReader.clsSearchEngineParameters = Nothing
		Dim blnSuccess As Boolean

		Try
			Console.WriteLine()

			If String.IsNullOrEmpty(strSearchEngineParamFileName) Then
				ShowWarningMessage("Search engine parameter file not defined; use /E to specify the filename")
				objSearchEngineParams = New PHRPReader.clsSearchEngineParameters(mPeptideHitResultType.ToString())
			Else
				ShowMessage("Loading Search Engine parameters")

				blnSuccess = objPHRPReader.PHRPParser.LoadSearchEngineParameters(strSearchEngineParamFileName, objSearchEngineParams)

				If Not blnSuccess Then
					objSearchEngineParams = New PHRPReader.clsSearchEngineParameters(mPeptideHitResultType.ToString())
				End If
			End If

			' Make sure mSearchEngineParams.ModInfo is up-to-date

			Dim strSpectrumKey As String
			Dim udtCurrentSpectrum As clsPepXMLWriter.udtSpectrumInfoType = New clsPepXMLWriter.udtSpectrumInfoType
			Dim blnMatchFound As Boolean

			For Each objItem In mPSMsBySpectrumKey

				strSpectrumKey = objItem.Key
				If mSpectrumInfo.TryGetValue(strSpectrumKey, udtCurrentSpectrum) Then

					For Each oPSMEntry As PHRPReader.clsPSM In objItem.Value
						If oPSMEntry.ModifiedResidues.Count > 0 Then
							For Each objResidue As PHRPReader.clsAminoAcidModInfo In oPSMEntry.ModifiedResidues

								' Check whether .ModDefinition is present in objSearchEngineParams.ModInfo
								blnMatchFound = False
								For Each objKnownMod As PHRPReader.clsModificationDefinition In objSearchEngineParams.ModInfo
									If objKnownMod Is objResidue.ModDefinition Then
										blnMatchFound = True
										Exit For
									End If
								Next

								If Not blnMatchFound Then
									objSearchEngineParams.ModInfo.Add(objResidue.ModDefinition)
								End If
							Next

						End If
					Next

				End If
			Next

		Catch ex As Exception
			HandleException("Error in LoadSearchEngineParameters", ex)
		End Try

		Return objSearchEngineParams

	End Function

	Protected Sub PreviewRequiredFiles(ByVal strInputFilePath As String, ByVal strDatasetName As String, ByVal ePeptideHitResultType As PHRPReader.clsPHRPReader.ePeptideHitResultType, ByVal blnLoadModsAndSeqInfo As Boolean, ByVal blnLoadMSGFResults As Boolean, ByVal blnLoadScanStats As Boolean, ByVal strSearchEngineParamFileName As String)
		Const PAD_WIDTH As Integer = 22

		Dim fiFileInfo As System.IO.FileInfo
		fiFileInfo = New System.IO.FileInfo(strInputFilePath)

		Console.WriteLine()
		If fiFileInfo.DirectoryName.Length > 40 Then
			ShowMessage("Data file folder: ")
			ShowMessage(fiFileInfo.DirectoryName)
			Console.WriteLine()
		Else
			ShowMessage("Data file folder: " & fiFileInfo.DirectoryName)
		End If

		ShowMessage("Data file: ".PadRight(PAD_WIDTH) & System.IO.Path.GetFileName(strInputFilePath))


		If blnLoadModsAndSeqInfo Then
			ShowMessage("ModSummary file: ".PadRight(PAD_WIDTH) & PHRPReader.clsPHRPReader.GetPHRPModSummaryFileName(ePeptideHitResultType, strDatasetName))
			ShowMessage("SeqInfo file: ".PadRight(PAD_WIDTH) & PHRPReader.clsPHRPReader.GetPHRPResultToSeqMapFileName(ePeptideHitResultType, strDatasetName))
			ShowMessage("SeqInfo file: ".PadRight(PAD_WIDTH) & PHRPReader.clsPHRPReader.GetPHRPSeqInfoFileName(ePeptideHitResultType, strDatasetName))
			ShowMessage("SeqInfo file: ".PadRight(PAD_WIDTH) & PHRPReader.clsPHRPReader.GetPHRPSeqToProteinMapFileName(ePeptideHitResultType, strDatasetName))
		End If

		If blnLoadMSGFResults Then
			ShowMessage("MSGF Results file: ".PadRight(PAD_WIDTH) & PHRPReader.clsPHRPReader.GetMSGFFileName(strInputFilePath))
		End If

		If blnLoadScanStats Then
			ShowMessage("ScanStats file: ".PadRight(PAD_WIDTH) & PHRPReader.clsPHRPReader.GetScanStatsFilename(mDatasetName))
			ShowMessage("ScanStats file: ".PadRight(PAD_WIDTH) & PHRPReader.clsPHRPReader.GetExtendedScanStatsFilename(mDatasetName))
		End If

		If Not String.IsNullOrEmpty(strSearchEngineParamFileName) Then
			ShowMessage("Search Engine Params: ".PadRight(PAD_WIDTH) & strSearchEngineParamFileName)

			If mPeptideHitResultType = PHRPReader.clsPHRPReader.ePeptideHitResultType.XTandem Then
				' Determine the additional files that will be required
				Dim lstFileNames As System.Collections.Generic.List(Of String)
				lstFileNames = PHRPReader.clsPHRPParserXTandem.GetAdditionalSearchEngineParamFileNames(System.IO.Path.Combine(fiFileInfo.DirectoryName, strSearchEngineParamFileName))

				For Each strFileName In lstFileNames
					ShowMessage("Search Engine Params: ".PadRight(PAD_WIDTH) & strFileName)
				Next

			End If
		End If

	End Sub

	''' <summary>
	''' Main processing function; calls ConvertPHRPDataToXML
	''' </summary>
	''' <param name="strInputFilePath">PHRP Input file path</param>
	''' <param name="strOutputFolderPath">Output folder path (if empty, then output file will be created in the same folder as the input file)</param>
	''' <param name="strParameterFilePath">Parameter file path</param>
	''' <param name="blnResetErrorCode">True to reset the error code prior to processing</param>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Overloads Overrides Function ProcessFile(ByVal strInputFilePath As String, ByVal strOutputFolderPath As String, ByVal strParameterFilePath As String, ByVal blnResetErrorCode As Boolean) As Boolean
		' Returns True if success, False if failure

		Dim ioFile As System.IO.FileInfo
		Dim strInputFilePathFull As String

		Dim blnSuccess As Boolean

		If blnResetErrorCode Then
			SetLocalErrorCode(ePeptideListToXMLErrorCodes.NoError)
		End If

		If Not LoadParameterFileSettings(strParameterFilePath) Then
			ShowErrorMessage("Parameter file load error: " & strParameterFilePath)

			If MyBase.ErrorCode = clsProcessFilesBaseClass.eProcessFilesErrorCodes.NoError Then
				MyBase.SetBaseClassErrorCode(clsProcessFilesBaseClass.eProcessFilesErrorCodes.InvalidParameterFile)
			End If
			Return False
		End If

		Try
			If strInputFilePath Is Nothing OrElse strInputFilePath.Length = 0 Then
				ShowMessage("Input file name is empty")
				MyBase.SetBaseClassErrorCode(clsProcessFilesBaseClass.eProcessFilesErrorCodes.InvalidInputFilePath)
			Else

				Console.WriteLine()
				If Not mPreviewMode Then
					ShowMessage("Parsing " & System.IO.Path.GetFileName(strInputFilePath))
				End If

				' Note that CleanupFilePaths() will update mOutputFolderPath, which is used by LogMessage()
				If Not CleanupFilePaths(strInputFilePath, strOutputFolderPath) Then
					MyBase.SetBaseClassErrorCode(clsProcessFilesBaseClass.eProcessFilesErrorCodes.FilePathError)
				Else

					If Not mPreviewMode Then
						MyBase.ResetProgress()
					End If

					Try
						' Obtain the full path to the input file
						ioFile = New System.IO.FileInfo(strInputFilePath)
						strInputFilePathFull = ioFile.FullName

						blnSuccess = ConvertPHRPDataToXML(strInputFilePathFull, strOutputFolderPath)

					Catch ex As Exception
						HandleException("Error calling ConvertToXML", ex)
					End Try
				End If
				End If
		Catch ex As Exception
			HandleException("Error in ProcessFile", ex)
		End Try

		Return blnSuccess

	End Function

	Protected Sub ShowWarningMessage(strWarningMessage As String)
		ShowMessage("Warning: " & strWarningMessage)
	End Sub

	Private Sub SetLocalErrorCode(ByVal eNewErrorCode As ePeptideListToXMLErrorCodes)
		SetLocalErrorCode(eNewErrorCode, False)
	End Sub

	Private Sub SetLocalErrorCode(ByVal eNewErrorCode As ePeptideListToXMLErrorCodes, ByVal blnLeaveExistingErrorCodeUnchanged As Boolean)

		If blnLeaveExistingErrorCodeUnchanged AndAlso mLocalErrorCode <> ePeptideListToXMLErrorCodes.NoError Then
			' An error code is already defined; do not change it
		Else
			mLocalErrorCode = eNewErrorCode

			If eNewErrorCode = ePeptideListToXMLErrorCodes.NoError Then
				If MyBase.ErrorCode = clsProcessFilesBaseClass.eProcessFilesErrorCodes.LocalizedError Then
					MyBase.SetBaseClassErrorCode(clsProcessFilesBaseClass.eProcessFilesErrorCodes.NoError)
				End If
			Else
				MyBase.SetBaseClassErrorCode(clsProcessFilesBaseClass.eProcessFilesErrorCodes.LocalizedError)
			End If
		End If

	End Sub

	Protected Function WriteCachedData(ByVal strInputFilePath As String, ByVal strOutputFilePath As String, ByRef objSearchEngineParams As PHRPReader.clsSearchEngineParameters) As Boolean

		Dim intSpectra As Integer
		Dim intPeptides As Integer

		Dim blnSuccess As Boolean

		Dim strSpectrumKey As String
		Dim udtCurrentSpectrum As clsPepXMLWriter.udtSpectrumInfoType = New clsPepXMLWriter.udtSpectrumInfoType

		Try
			Console.WriteLine()
			ShowMessage("Creating PepXML file at " & System.IO.Path.GetFileName(strOutputFilePath))

			mXMLWriter = New clsPepXMLWriter(mDatasetName, mFastaFilePath, objSearchEngineParams, strInputFilePath, strOutputFilePath)

			If Not mXMLWriter.IsWritable Then
				ShowErrorMessage("XMLWriter is not writable; aborting")
				Return False
			End If

			intSpectra = 0
			intPeptides = 0
			For Each objItem In mPSMsBySpectrumKey

				strSpectrumKey = objItem.Key
				If mSpectrumInfo.TryGetValue(strSpectrumKey, udtCurrentSpectrum) Then
					intSpectra += 1
					intPeptides += objItem.Value.Count

					mXMLWriter.WriteSpectrum(udtCurrentSpectrum, objItem.Value, mSeqToProteinMapCached)
				Else
					ShowErrorMessage("Spectrum key '" & strSpectrumKey & "' not found in mSpectrumInfo; this is unexected")
				End If

				If intPeptides Mod 2500 = 0 Then
					Console.Write(".")
				End If
			Next

			If intPeptides > 2500 Then
				Console.WriteLine()
			End If

			mXMLWriter.CloseDocument()

			Console.WriteLine()
			ShowMessage("PepXML file created with " & intSpectra.ToString("0,000") & " spectra and " & intPeptides.ToString("0,000") & " peptides")

			blnSuccess = True

		Catch ex As Exception
			ShowErrorMessage("Error Reading source file in WriteCachedData: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess
	End Function

	Private Sub mXMLWriter_ErrorEvent(Message As String) Handles mXMLWriter.ErrorEvent
		ShowErrorMessage(Message)
	End Sub

	Private Sub mPHRPReader_ErrorEvent(strErrorMessage As String) Handles mPHRPReader.ErrorEvent
		ShowErrorMessage(strErrorMessage)
	End Sub

	Private Sub mPHRPReader_MessageEvent(strMessage As String) Handles mPHRPReader.MessageEvent
		ShowMessage(strMessage)
	End Sub

	Private Sub mPHRPReader_WarningEvent(strWarningMessage As String) Handles mPHRPReader.WarningEvent
		ShowWarningMessage(strWarningMessage)		
	End Sub
End Class
