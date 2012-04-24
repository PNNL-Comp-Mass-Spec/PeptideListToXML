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
		MyBase.mFileDate = "April 23, 2012"
        InitializeLocalVariables()
    End Sub

#Region "Constants and Enums"

    Public Const XML_SECTION_OPTIONS As String = "PeptideListToXMLOptions"

	' Future enum; mzIdentML is not yet supported
	'Public Enum ePeptideListOutputFormat
	'    PepXML = 0
	'    mzIdentML = 1
	'End Enum

    ' Error codes specialized for this class
    Public Enum ePeptideListToXMLErrorCodes
        NoError = 0
        ErrorReadingInputFile = 1
        ErrorWritingOutputFile = 2
        UnspecifiedError = -1
    End Enum

#End Region

#Region "Structures"

#End Region

#Region "Classwide Variables"

	' Future enum; mzIdentML is not yet supported
	'Protected mOutputFormat As clsPeptideListToXML.ePeptideListOutputFormat

	Protected WithEvents mPHRPReader As PHRPReader.clsPHRPReader

	' Note that DatasetName is auto-determined via ConvertPHRPDataToXML()
	Protected mDatasetName As String
	Protected mPeptideHitResultType As PHRPReader.clsPHRPReader.ePeptideHitResultType

	Protected mFastaFilePath As String
	Protected mSearchEngineParamFileName As String

	' This dictionary tracks the PSMs (hits) for each spectrum
	' The key is the Spectrum Key string (dataset, start scan, end scan, charge)
	Protected mPSMsBySpectrumKey As System.Collections.Generic.Dictionary(Of String, System.Collections.Generic.List(Of PHRPReader.clsPSM))

	' This dictionary tracks the spectrum info
	' The key is the Spectrum Key string (dataset, start scan, end scan, charge)
	Protected mSpectrumInfo As System.Collections.Generic.Dictionary(Of String, clsPepXMLWriter.udtSpectrumInfoType)

	Private mLocalErrorCode As ePeptideListToXMLErrorCodes
#End Region

#Region "Processing Options Interface Functions"

	Public ReadOnly Property DatasetName() As String
		Get
			Return mDatasetName
		End Get
	End Property

	Public Property FastaFilePath() As String
		Get
			Return mFastaFilePath
		End Get
		Set(value As String)
			mFastaFilePath = value
		End Set
	End Property

	Public ReadOnly Property LocalErrorCode() As ePeptideListToXMLErrorCodes
		Get
			Return mLocalErrorCode
		End Get
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

	Public Function ConvertPHRPDataToXML(ByVal strInputFilePath As String, ByVal strOutputFolderPath As String) As Boolean

		Dim blnSuccess As Boolean
		Dim objSearchEngineParams As PHRPReader.clsSearchEngineParameters

		' Note that CachePHRPData() will update these variables
		mDatasetName = "Unknown"
		mPeptideHitResultType = PHRPReader.clsPHRPReader.ePeptideHitResultType.Unknown

		blnSuccess = CachePHRPData(strInputFilePath)

		If blnSuccess Then

			objSearchEngineParams = LoadSearchEngineParameters(mSearchEngineParamFileName)

			blnSuccess = WriteCachedData(strOutputFolderPath, objSearchEngineParams)
		End If

		Return blnSuccess
	End Function

	Protected Function CachePHRPData(ByVal strInputFilePath As String) As Boolean

		Dim blnSuccess As Boolean
		Dim strSpectrumKey As String
		Dim udtSpectrumInfo As clsPepXMLWriter.udtSpectrumInfoType
		Dim objPSMs As System.Collections.Generic.List(Of PHRPReader.clsPSM) = Nothing

		Try

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

			Using mPHRPReader = New PHRPReader.clsPHRPReader(strInputFilePath)
				mPHRPReader.SkipDuplicatePSMs = True

				mDatasetName = mPHRPReader.DatasetName
				mPeptideHitResultType = mPHRPReader.PeptideHitResultType

				If String.IsNullOrEmpty(mDatasetName) Then
					mDatasetName = "Unknown"
					ShowMessage("Warning, unable to determine the dataset name from the input file path")
				End If

				While mPHRPReader.MoveNext

					Dim objCurrentPSM As PHRPReader.clsPSM = mPHRPReader.CurrentPSM

					strSpectrumKey = GetSpectrumKey(objCurrentPSM)

					If Not mSpectrumInfo.ContainsKey(strSpectrumKey) Then
						' New spectrum; add a new entry to mSpectrumInfo
						udtSpectrumInfo = New clsPepXMLWriter.udtSpectrumInfoType
						udtSpectrumInfo.SpectrumName = strSpectrumKey
						udtSpectrumInfo.StartScan = objCurrentPSM.ScanNumber
						udtSpectrumInfo.EndScan = objCurrentPSM.ScanNumber
						udtSpectrumInfo.PrecursorNeutralMass = objCurrentPSM.PrecursorNeutralMass
						udtSpectrumInfo.AssumedCharge = objCurrentPSM.Charge
						udtSpectrumInfo.ElutionTimeMinutes = objCurrentPSM.ElutionTimeMinutes
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


				End While

			End Using

			blnSuccess = True

		Catch ex As Exception
			ShowErrorMessage("Error Reading source file in CachePHRPData: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	Public Overrides Function GetDefaultExtensionsToParse() As String()
		Dim strExtensionsToParse(0) As String

		strExtensionsToParse(0) = ".fasta"

		Return strExtensionsToParse

	End Function

	Public Overrides Function GetErrorMessage() As String
		' Returns "" if no error

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
		Return mDatasetName & "." & CurrentPSM.ScanNumber & "." & CurrentPSM.ScanNumber & CurrentPSM.Charge
	End Function

	Private Sub InitializeLocalVariables()
		mLocalErrorCode = ePeptideListToXMLErrorCodes.NoError

		mDatasetName = "Unknown"
		mPeptideHitResultType = PHRPReader.clsPHRPReader.ePeptideHitResultType.Unknown

		mFastaFilePath = String.Empty
		mSearchEngineParamFileName = String.Empty

	End Sub

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
					' Define options here

					' Me.ProcessingOption = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "ProcessingOption", Me.ProcessingOption)
				End If
			End If

		Catch ex As Exception
			HandleException("Error in LoadParameterFileSettings", ex)
			Return False
		End Try

		Return True

	End Function

	Protected Function LoadSearchEngineParameters(ByVal strSearchEngineParamFileName As String) As PHRPReader.clsSearchEngineParameters
		Dim objSearchEngineParams As PHRPReader.clsSearchEngineParameters = Nothing
		Dim blnSuccess As Boolean

		Try
			blnSuccess = mPHRPReader.PHRPParser.LoadSearchEngineParameters(strSearchEngineParamFileName, objSearchEngineParams)

			If Not blnSuccess Then
				objSearchEngineParams = New PHRPReader.clsSearchEngineParameters(mPHRPReader.PeptideHitResultType.ToString())
			End If
		Catch ex As Exception
			HandleException("Error in LoadSearchEngineParameters", ex)
		End Try

		Return objSearchEngineParams

	End Function

	' Main processing function -- Calls ParseProteinFile
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
				Console.WriteLine("Parsing " & System.IO.Path.GetFileName(strInputFilePath))

				' Note that CleanupFilePaths() will update mOutputFolderPath, which is used by LogMessage()
				If Not CleanupFilePaths(strInputFilePath, strOutputFolderPath) Then
					MyBase.SetBaseClassErrorCode(clsProcessFilesBaseClass.eProcessFilesErrorCodes.FilePathError)
				Else

					MyBase.ResetProgress()

					Try
						' Obtain the full path to the input file
						ioFile = New System.IO.FileInfo(strInputFilePath)
						strInputFilePathFull = ioFile.FullName

						blnSuccess = ConvertPHRPDataToXML(strInputFilePathFull, strOutputFolderPath)

						If blnSuccess Then
							ShowMessage(String.Empty, False)
						Else
							SetLocalErrorCode(ePeptideListToXMLErrorCodes.UnspecifiedError)
							ShowErrorMessage("Error")
						End If

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

	Protected Function WriteCachedData(ByVal strOutputFilePath As String, ByRef objSearchEngineParams As PHRPReader.clsSearchEngineParameters) As Boolean

		Dim objXMLWriter As clsPepXMLWriter
		Dim blnSuccess As Boolean

		Dim strSpectrumKey As String
		Dim udtCurrentSpectrum As clsPepXMLWriter.udtSpectrumInfoType = New clsPepXMLWriter.udtSpectrumInfoType

		Try

			objXMLWriter = New clsPepXMLWriter(mDatasetName, mFastaFilePath, objSearchEngineParams, strOutputFilePath)

			If Not objXMLWriter.IsWritable Then
				ShowErrorMessage("XMLWriter is not writable; aborting")
				Return False
			End If

			For Each objItem In mPSMsBySpectrumKey

				strSpectrumKey = objItem.Key
				If mSpectrumInfo.TryGetValue(strSpectrumKey, udtCurrentSpectrum) Then
					objXMLWriter.WriteSpectrum(udtCurrentSpectrum, objItem.Value)
				Else
					ShowErrorMessage("Warning, spectrum key not found in mSpectrumInfo: " & strSpectrumKey)
				End If

			Next

			objXMLWriter.CloseDocument()

			blnSuccess = True

		Catch ex As Exception
			ShowErrorMessage("Error Reading source file in WriteCachedData: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess
	End Function

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

End Class
