Option Strict On

' This class will read a tab-delimited text file with peptides and scores
' and create a new PepXML or mzIdentML file with the peptides
'
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
'
' Started April 13, 2012

Imports System.IO
Imports PRISM

Public Class clsPeptideListToXML
    Inherits FileProcessor.ProcessFilesBase

    ' Ignore Spelling: mzIdentML, Wiff

    Public Sub New()
        MyBase.mFileDate = "June 17, 2021"
        InitializeLocalVariables()
    End Sub

#Region "Constants and Enums"

    Public Const XML_SECTION_OPTIONS As String = "PeptideListToXMLOptions"
    Public Const DEFAULT_HITS_PER_SPECTRUM As Integer = 3
    Public Const DEFAULT_MAX_PROTEINS_PER_PSM As Integer = 100

    Protected Const PREVIEW_PAD_WIDTH As Integer = 22

    ' Future enum; mzIdentML is not yet supported
    'Public Enum PeptideListOutputFormat
    '    PepXML = 0
    '    mzIdentML = 1
    'End Enum

    ''' <summary>
    ''' Error codes specialized for this class
    ''' </summary>
    ''' <remarks></remarks>
    Public Enum PeptideListToXMLErrorCodes
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

#Region "Class wide Variables"

    ' Future enum; mzIdentML is not yet supported
    'Protected mOutputFormat As clsPeptideListToXML.PeptideListOutputFormat

    Protected mPHRPReader As PHRPReader.ReaderFactory
    Protected mXMLWriter As clsPepXMLWriter

    ' Note that DatasetName is auto-determined via ConvertPHRPDataToXML()
    Protected mDatasetName As String
    Protected mPeptideHitResultType As PHRPReader.PeptideHitResultTypes
    Protected mSeqToProteinMapCached As SortedList(Of Integer, List(Of PHRPReader.Data.ProteinInfo))

    ' Note that FastaFilePath will be ignored if the Search Engine Param File exists and it contains a fasta file name
    Protected mFastaFilePath As String
    Protected mSearchEngineParamFileName As String
    Protected mHitsPerSpectrum As Integer                ' Number of hits per spectrum to store; 0 means to store all hits
    Protected mPreviewMode As Boolean

    Protected mSkipXPeptides As Boolean
    Protected mTopHitOnly As Boolean
    Protected mMaxProteinsPerPSM As Integer

    Protected mPeptideFilterFilePath As String
    Protected mChargeFilterList As List(Of Integer)

    Protected mLoadModsAndSeqInfo As Boolean
    Protected mLoadMSGFResults As Boolean
    Protected mLoadScanStats As Boolean

    ' This dictionary tracks the PSMs (hits) for each spectrum
    ' The key is the Spectrum Key string (dataset, start scan, end scan, charge)
    Protected mPSMsBySpectrumKey As Dictionary(Of String, List(Of PHRPReader.Data.PSM))

    ' This dictionary tracks the spectrum info
    ' The key is the Spectrum Key string (dataset, start scan, end scan, charge)
    Protected mSpectrumInfo As Dictionary(Of String, clsPepXMLWriter.udtSpectrumInfoType)

    Private mLocalErrorCode As PeptideListToXMLErrorCodes
#End Region

#Region "Processing Options Interface Functions"

    Public Property ChargeFilterList As List(Of Integer)
        Get
            Return mChargeFilterList
        End Get
        Set
            If Value Is Nothing Then
                mChargeFilterList = New List(Of Integer)
            Else
                mChargeFilterList = Value
            End If
        End Set
    End Property

    ' ReSharper disable once UnusedMember.Global
    ''' <summary>
    ''' Dataset name; auto-determined by the PHRP Reader class
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property DatasetName As String
        Get
            Return mDatasetName
        End Get
    End Property

    ''' <summary>
    ''' Fasta file path to store in the pepXML file
    ''' Ignored if the Search Engine Param File exists and it contains a fasta file name (typically the case for SEQUEST and X!Tandem)
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Property FastaFilePath As String
        Get
            Return mFastaFilePath
        End Get
        Set
            mFastaFilePath = Value
        End Set
    End Property

    ''' <summary>
    ''' Number of peptides per spectrum to store in the PepXML file; 0 means store all hits
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Property HitsPerSpectrum As Integer
        Get
            Return mHitsPerSpectrum
        End Get
        Set
            mHitsPerSpectrum = Value
        End Set
    End Property

    Public Property LoadModsAndSeqInfo As Boolean
        Get
            Return mLoadModsAndSeqInfo
        End Get
        Set
            mLoadModsAndSeqInfo = Value
        End Set
    End Property

    Public Property LoadMSGFResults As Boolean
        Get
            Return mLoadMSGFResults
        End Get
        Set
            mLoadMSGFResults = Value
        End Set
    End Property

    Public Property LoadScanStats As Boolean
        Get
            Return mLoadScanStats
        End Get
        Set
            mLoadScanStats = Value
        End Set
    End Property

    ' ReSharper disable once UnusedMember.Global
    ''' <summary>
    ''' Local error code
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property LocalErrorCode As PeptideListToXMLErrorCodes
        Get
            Return mLocalErrorCode
        End Get
    End Property

    Public Property MaxProteinsPerPSM As Integer
        Get
            Return mMaxProteinsPerPSM
        End Get
        Set
            mMaxProteinsPerPSM = Value
        End Set
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
        Set
            mPreviewMode = Value
        End Set
    End Property

    ''' <summary>
    ''' Name of the parameter file used by the search engine that produced the results file that we are parsing
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Property SearchEngineParamFileName As String
        Get
            Return mSearchEngineParamFileName
        End Get
        Set
            mSearchEngineParamFileName = Value
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
        Set
            mSkipXPeptides = Value
        End Set
    End Property

    ''' <summary>
    ''' If True, then only keeps the top-scoring peptide for each scan number
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks>If the scan has multiple charges, the output file will still only have one peptide listed for that scan number</remarks>
    Public Property TopHitOnly As Boolean
        Get
            Return mTopHitOnly
        End Get
        Set
            mTopHitOnly = Value
        End Set
    End Property

    Public Property PeptideFilterFilePath As String
        Get
            Return mPeptideFilterFilePath
        End Get
        Set
            If String.IsNullOrEmpty(Value) Then
                mPeptideFilterFilePath = String.Empty
            Else
                mPeptideFilterFilePath = Value
            End If
        End Set
    End Property

    ' Future enum; mzIdentML is not yet supported
    'Public Property OutputFormat() As PeptideListOutputFormat
    '    Get
    '        Return mOutputFormat
    '    End Get
    '    Set(value As PeptideListOutputFormat)
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
    Public Function ConvertPHRPDataToXML(strInputFilePath As String, strOutputFolderPath As String) As Boolean

        Dim objSearchEngineParams As PHRPReader.Data.SearchEngineParameters = Nothing
        Dim strOutputFilePath As String
        Dim blnSuccess As Boolean

        ' Note that CachePHRPData() will update these variables
        mDatasetName = "Unknown"
        mPeptideHitResultType = PHRPReader.PeptideHitResultTypes.Unknown

        blnSuccess = CachePHRPData(strInputFilePath, objSearchEngineParams)

        If Not blnSuccess Then Return False

        If mPreviewMode Then
            PreviewRequiredFiles(strInputFilePath, mDatasetName, mPeptideHitResultType, mLoadModsAndSeqInfo, mLoadMSGFResults, mLoadScanStats, mSearchEngineParamFileName)
        Else
            strOutputFilePath = Path.Combine(strOutputFolderPath, mDatasetName & ".pepXML")
            blnSuccess = WriteCachedData(strInputFilePath, strOutputFilePath, objSearchEngineParams)
        End If

        Return blnSuccess

    End Function

    Protected Function CachePHRPData(strInputFilePath As String, ByRef objSearchEngineParams As PHRPReader.Data.SearchEngineParameters) As Boolean

        Dim blnSuccess As Boolean
        Dim udtSpectrumInfo As clsPepXMLWriter.udtSpectrumInfoType
        Dim blnSkipPeptide As Boolean

        Dim lstPeptidesToFilterOn As SortedSet(Of String)
        Dim objPSMs As List(Of PHRPReader.Data.PSM) = Nothing

        ' Keys in this dictionary are scan numbers
        Dim dctBestPSMByScan As Dictionary(Of Integer, clsPSMInfo)

        Dim intPeptidesStored As Integer

        Try
            If mPreviewMode Then
                ResetProgress("Finding required files")
            Else
                ResetProgress("Caching PHRP data")
            End If

            If mPSMsBySpectrumKey Is Nothing Then
                mPSMsBySpectrumKey = New Dictionary(Of String, List(Of PHRPReader.Data.PSM))
            Else
                mPSMsBySpectrumKey.Clear()
            End If

            If mSpectrumInfo Is Nothing Then
                mSpectrumInfo = New Dictionary(Of String, clsPepXMLWriter.udtSpectrumInfoType)
            Else
                mSpectrumInfo.Clear()
            End If

            dctBestPSMByScan = New Dictionary(Of Integer, clsPSMInfo)

            If mChargeFilterList Is Nothing Then mChargeFilterList = New List(Of Integer)

            intPeptidesStored = 0

            Dim oStartupOptions = New PHRPReader.StartupOptions() With {
                .LoadModsAndSeqInfo = mLoadModsAndSeqInfo,
                .LoadMSGFResults = mLoadMSGFResults,
                .LoadScanStatsData = mLoadScanStats,
                .MaxProteinsPerPSM = MaxProteinsPerPSM
            }

            mPHRPReader = New PHRPReader.ReaderFactory(strInputFilePath, oStartupOptions)
            RegisterEvents(mPHRPReader)

            mDatasetName = mPHRPReader.DatasetName
            mPeptideHitResultType = mPHRPReader.PeptideHitResultType
            mSeqToProteinMapCached = mPHRPReader.SeqToProteinMap

            lstPeptidesToFilterOn = New SortedSet(Of String)
            If Not String.IsNullOrWhiteSpace(mPeptideFilterFilePath) Then
                blnSuccess = LoadPeptideFilterFile(mPeptideFilterFilePath, lstPeptidesToFilterOn)
                If Not blnSuccess Then Return False
            End If

            If mPreviewMode Then
                ' We can exit this function now since we have determined the dataset name and peptide hit result type
                Return True
            End If

            ' Report any errors cached during instantiation of mPHRPReader
            For Each strMessage In mPHRPReader.ErrorMessages.Distinct()
                ShowErrorMessage(strMessage)
            Next

            ' Report any warnings cached during instantiation of mPHRPReader
            For Each strMessage In mPHRPReader.WarningMessages.Distinct()
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
                    ShowMessage("  ... unable to determine elution times; use the /NoScanStats switch to avoid this error")
                ElseIf strMessage.Contains("ModSummary file not found") Then
                    ShowMessage("  ... ModSummary file was not found; will infer modification details")
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

                Dim objCurrentPSM As PHRPReader.Data.PSM = mPHRPReader.CurrentPSM

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

                If Not blnSkipPeptide AndAlso lstPeptidesToFilterOn.Count > 0 Then
                    If Not lstPeptidesToFilterOn.Contains(objCurrentPSM.PeptideCleanSequence) Then
                        blnSkipPeptide = True
                    End If
                End If

                If Not blnSkipPeptide AndAlso mChargeFilterList.Count > 0 Then
                    If Not mChargeFilterList.Contains(objCurrentPSM.Charge) Then
                        blnSkipPeptide = True
                    End If
                End If

                If Not blnSkipPeptide Then
                    Dim strSpectrumKey As String
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
                        udtSpectrumInfo.NativeID = ConstructNativeID(objCurrentPSM.ScanNumberStart)

                        mSpectrumInfo.Add(strSpectrumKey, udtSpectrumInfo)
                    End If

                    If mPSMsBySpectrumKey.TryGetValue(strSpectrumKey, objPSMs) Then
                        objPSMs.Add(objCurrentPSM)
                    Else
                        objPSMs = New List(Of PHRPReader.Data.PSM) From {
                            objCurrentPSM
                        }
                        mPSMsBySpectrumKey.Add(strSpectrumKey, objPSMs)
                    End If

                    If mTopHitOnly Then
                        Dim objBestPSMInfo As clsPSMInfo = Nothing
                        Dim objComparisonPSMInfo = New clsPSMInfo(strSpectrumKey, objCurrentPSM)

                        If dctBestPSMByScan.TryGetValue(objCurrentPSM.ScanNumberStart, objBestPSMInfo) Then
                            If objComparisonPSMInfo.MSGFSpecProb < objBestPSMInfo.MSGFSpecProb Then
                                ' We have found a better scoring peptide for this scan
                                dctBestPSMByScan(objCurrentPSM.ScanNumberStart) = objComparisonPSMInfo
                            End If
                        Else
                            dctBestPSMByScan.Add(objCurrentPSM.ScanNumberStart, objComparisonPSMInfo)
                        End If

                    End If

                    intPeptidesStored += 1

                End If

                UpdateProgress(mPHRPReader.PercentComplete)

            End While

            OperationComplete()
            Console.WriteLine()

            Dim strFilterMessage As String = String.Empty
            If lstPeptidesToFilterOn.Count > 0 Then
                strFilterMessage = " (filtered using " & lstPeptidesToFilterOn.Count & " peptides in " & Path.GetFileName(mPeptideFilterFilePath) & ")"
            End If

            If mTopHitOnly Then
                ' Update mPSMsBySpectrumKey to contain the best hit for each scan number (regardless of charge)

                Dim intCountAtStart As Integer = mPSMsBySpectrumKey.Count
                mPSMsBySpectrumKey.Clear()

                For Each item In dctBestPSMByScan
                    Dim lstPSMs = New List(Of PHRPReader.Data.PSM) From {
                        item.Value.PSM
                    }
                    mPSMsBySpectrumKey.Add(item.Value.SpectrumKey, lstPSMs)
                Next
                intPeptidesStored = mPSMsBySpectrumKey.Count

                ShowMessage(" ... cached " & intPeptidesStored.ToString("#,##0") & " PSMs" & strFilterMessage)
                ShowMessage(" ... filtered out " & (intCountAtStart - intPeptidesStored).ToString("#,##0") & " PSMs to only retain the top hit for each scan (regardless of charge)")
            Else
                ShowMessage(" ... cached " & intPeptidesStored.ToString("#,##0") & " PSMs" & strFilterMessage)
            End If

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
                    SetLocalErrorCode(PeptideListToXMLErrorCodes.ModSummaryFileNotFound)
                Else
                    SetLocalErrorCode(PeptideListToXMLErrorCodes.ErrorReadingInputFile)
                    ShowMessage(ex.StackTrace)
                End If
            End If

            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    ''' <summary>
    ''' Constructs a Thermo-style nativeID string for the given spectrum
    ''' This allows for linking up with data in .mzML files
    ''' </summary>
    ''' <param name="intScanNumber"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Protected Function ConstructNativeID(intScanNumber As Integer) As String
        ' Examples:
        ' Most Thermo raw files: "controllerType=0 controllerNumber=1 scan=6"
        ' Thermo raw with PQD spectra: "controllerType=1 controllerNumber=1 scan=6"
        ' Wiff files: "sample=1 period=1 cycle=123 experiment=2"
        ' Waters files: "function=2 process=0 scan=123

        ' For now, we're assuming all data processed by this program is from Thermo raw files

        Return "controllerType=0 controllerNumber=1 scan=" & intScanNumber.ToString()

    End Function

    ' ReSharper disable once UnusedMember.Global
    ''' <summary>
    ''' Returns the default file extensions that this class knows how to parse
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Overrides Function GetDefaultExtensionsToParse() As IList(Of String)
        Dim extensionsToParse = New List(Of String) From {
            PHRPReader.Reader.SequestSynFileReader.GetPHRPSynopsisFileName(String.Empty),
            PHRPReader.Reader.XTandemSynFileReader.GetPHRPSynopsisFileName(String.Empty),
            PHRPReader.Reader.MSGFPlusSynFileReader.GetPHRPSynopsisFileName(String.Empty),
            PHRPReader.Reader.InspectSynFileReader.GetPHRPSynopsisFileName(String.Empty),
            PHRPReader.Reader.MODaSynFileReader.GetPHRPSynopsisFileName(String.Empty),
            PHRPReader.Reader.MODPlusSynFileReader.GetPHRPSynopsisFileName(String.Empty)
        }

        Return extensionsToParse

    End Function

    ''' <summary>
    ''' Returns the error message; empty string if no error
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Overrides Function GetErrorMessage() As String

        Dim strErrorMessage As String

        If MyBase.ErrorCode = ProcessFilesErrorCodes.LocalizedError Or
           MyBase.ErrorCode = ProcessFilesErrorCodes.NoError Then
            Select Case mLocalErrorCode
                Case PeptideListToXMLErrorCodes.NoError
                    strErrorMessage = ""

                Case PeptideListToXMLErrorCodes.ErrorReadingInputFile
                    strErrorMessage = "Error reading input file"

                Case PeptideListToXMLErrorCodes.ErrorWritingOutputFile
                    strErrorMessage = "Error writing to the output file"

                Case PeptideListToXMLErrorCodes.ModSummaryFileNotFound
                    strErrorMessage = "ModSummary file not found; use the /NoMods switch to avoid this error (though modified peptides will not be stored properly)"

                Case PeptideListToXMLErrorCodes.SeqInfoFileNotFound
                    strErrorMessage = "SeqInfo file not found; use the /NoMods switch to avoid this error (though modified peptides will not be stored properly)"

                Case PeptideListToXMLErrorCodes.MSGFStatsFileNotFound
                    strErrorMessage = "MSGF file not found; use the /NoMSGF switch to avoid this error"

                Case PeptideListToXMLErrorCodes.ScanStatsFileNotFound
                    strErrorMessage = "MASIC ScanStats file not found; use the /NoScanStats switch to avoid this error"

                Case PeptideListToXMLErrorCodes.UnspecifiedError
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

    Protected Function GetSpectrumKey(ByRef CurrentPSM As PHRPReader.Data.PSM) As String
        Return mDatasetName & "." & CurrentPSM.ScanNumberStart & "." & CurrentPSM.ScanNumberEnd & "." & CurrentPSM.Charge
    End Function

    Private Sub InitializeLocalVariables()
        mLocalErrorCode = PeptideListToXMLErrorCodes.NoError

        mDatasetName = "Unknown"
        mPeptideHitResultType = PHRPReader.PeptideHitResultTypes.Unknown

        mFastaFilePath = String.Empty
        mSearchEngineParamFileName = String.Empty

        mHitsPerSpectrum = DEFAULT_HITS_PER_SPECTRUM
        mSkipXPeptides = False
        mTopHitOnly = False
        mMaxProteinsPerPSM = DEFAULT_MAX_PROTEINS_PER_PSM

        mPeptideFilterFilePath = String.Empty
        mChargeFilterList = New List(Of Integer)

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
    Public Function LoadParameterFileSettings(strParameterFilePath As String) As Boolean

        Dim objSettingsFile As New XmlSettingsFileAccessor

        Try

            If strParameterFilePath Is Nothing OrElse strParameterFilePath.Length = 0 Then
                ' No parameter file specified; nothing to load
                Return True
            End If

            If Not File.Exists(strParameterFilePath) Then
                ' See if strParameterFilePath points to a file in the same directory as the application
                strParameterFilePath = Path.Combine(GetAppDirectoryPath(), Path.GetFileName(strParameterFilePath))
                If Not File.Exists(strParameterFilePath) Then
                    MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.ParameterFileNotFound)
                    Return False
                End If
            End If

            If objSettingsFile.LoadSettings(strParameterFilePath) Then
                If Not objSettingsFile.SectionPresent(XML_SECTION_OPTIONS) Then
                    ShowErrorMessage("The node '<section name=""" & XML_SECTION_OPTIONS & """> was not found in the parameter file: " & strParameterFilePath)
                    MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile)
                    Return False
                Else

                    Me.FastaFilePath = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "FastaFilePath", Me.FastaFilePath)
                    Me.SearchEngineParamFileName = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "SearchEngineParamFileName", Me.SearchEngineParamFileName)
                    Me.HitsPerSpectrum = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "HitsPerSpectrum", Me.HitsPerSpectrum)
                    Me.SkipXPeptides = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "SkipXPeptides", Me.SkipXPeptides)
                    Me.TopHitOnly = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "TopHitOnly", Me.TopHitOnly)
                    Me.MaxProteinsPerPSM = objSettingsFile.GetParam(XML_SECTION_OPTIONS, "MaxProteinsPerPSM", Me.MaxProteinsPerPSM)

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

    Protected Function LoadPeptideFilterFile(strInputFilePath As String, ByRef lstPeptides As SortedSet(Of String)) As Boolean

        Try
            If lstPeptides Is Nothing Then
                lstPeptides = New SortedSet(Of String)
            Else
                lstPeptides.Clear()
            End If

            If Not File.Exists(strInputFilePath) Then
                ShowErrorMessage("Peptide filter file not found: " & strInputFilePath)
                SetLocalErrorCode(PeptideListToXMLErrorCodes.ErrorReadingInputFile)
                Return False
            End If

            If mPreviewMode Then
                ShowMessage("Peptide Filter file: ".PadRight(PREVIEW_PAD_WIDTH) & PHRPReader.ReaderFactory.GetMSGFFileName(strInputFilePath))
                Return True
            End If

            Using reader = New StreamReader(New FileStream(strInputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))

                Do While Not reader.EndOfStream
                    Dim dataLine = reader.ReadLine

                    If Not String.IsNullOrWhiteSpace(dataLine) Then
                        ' Remove any text present after a tab character
                        Dim tabIndex = dataLine.IndexOf(ControlChars.Tab)
                        If tabIndex > 0 Then
                            dataLine = dataLine.Substring(0, tabIndex)
                        End If

                        If Not String.IsNullOrWhiteSpace(dataLine) Then
                            dataLine = PHRPReader.PeptideCleavageStateCalculator.ExtractCleanSequenceFromSequenceWithMods(dataLine, True)
                            lstPeptides.Add(dataLine)
                        End If
                    End If
                Loop
            End Using

        Catch ex As Exception
            HandleException("Error in LoadPeptideFilterFile", ex)
            Return False
        End Try

        Return True

    End Function

    Protected Function LoadSearchEngineParameters(ByRef reader As PHRPReader.ReaderFactory, strSearchEngineParamFileName As String) As PHRPReader.Data.SearchEngineParameters
        Dim objSearchEngineParams As PHRPReader.Data.SearchEngineParameters = Nothing
        Dim blnSuccess As Boolean

        Try
            Console.WriteLine()

            If String.IsNullOrEmpty(strSearchEngineParamFileName) Then
                ShowWarningMessage("Search engine parameter file not defined; use /E to specify the filename")
                objSearchEngineParams = New PHRPReader.Data.SearchEngineParameters(mPeptideHitResultType.ToString())
            Else
                ShowMessage("Loading Search Engine parameters")

                blnSuccess = reader.SynFileReader.LoadSearchEngineParameters(strSearchEngineParamFileName, objSearchEngineParams)

                If Not blnSuccess Then
                    objSearchEngineParams = New PHRPReader.Data.SearchEngineParameters(mPeptideHitResultType.ToString())
                End If
            End If

            ' Make sure mSearchEngineParams.ModInfo is up-to-date

            Dim strSpectrumKey As String
            Dim udtCurrentSpectrum = New clsPepXMLWriter.udtSpectrumInfoType
            Dim blnMatchFound As Boolean

            For Each objItem In mPSMsBySpectrumKey

                strSpectrumKey = objItem.Key
                If mSpectrumInfo.TryGetValue(strSpectrumKey, udtCurrentSpectrum) Then

                    For Each oPSMEntry As PHRPReader.Data.PSM In objItem.Value
                        If oPSMEntry.ModifiedResidues.Count > 0 Then
                            For Each objResidue As PHRPReader.Data.AminoAcidModInfo In oPSMEntry.ModifiedResidues

                                ' Check whether objResidue.ModDefinition is present in objSearchEngineParams.ModInfo
                                blnMatchFound = False
                                For Each objKnownMod As PHRPReader.Data.ModificationDefinition In objSearchEngineParams.ModList
                                    If objKnownMod.EquivalentMassTypeTagAtomAndResidues(objResidue.ModDefinition) Then
                                        blnMatchFound = True
                                        Exit For
                                    End If
                                Next

                                If Not blnMatchFound Then
                                    objSearchEngineParams.ModList.Add(objResidue.ModDefinition)
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

    Protected Sub PreviewRequiredFiles(strInputFilePath As String, strDatasetName As String, PeptideHitResultTypes As PHRPReader.PeptideHitResultTypes, blnLoadModsAndSeqInfo As Boolean, blnLoadMSGFResults As Boolean, blnLoadScanStats As Boolean, strSearchEngineParamFileName As String)

        Dim fiFileInfo = New FileInfo(strInputFilePath)

        Console.WriteLine()
        If fiFileInfo.DirectoryName.Length > 40 Then
            ShowMessage("Data file folder: ")
            ShowMessage(fiFileInfo.DirectoryName)
            Console.WriteLine()
        Else
            ShowMessage("Data file folder: " & fiFileInfo.DirectoryName)
        End If

        ShowMessage("Data file: ".PadRight(PREVIEW_PAD_WIDTH) & Path.GetFileName(strInputFilePath))


        If blnLoadModsAndSeqInfo Then
            ShowMessage("ModSummary file: ".PadRight(PREVIEW_PAD_WIDTH) & PHRPReader.ReaderFactory.GetPHRPModSummaryFileName(PeptideHitResultTypes, strDatasetName))

            If fiFileInfo.Name.ToLower() = PHRPReader.ReaderFactory.GetPHRPSynopsisFileName(PeptideHitResultTypes, strDatasetName).ToLower() Then
                ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) & PHRPReader.ReaderFactory.GetPHRPResultToSeqMapFileName(PeptideHitResultTypes, strDatasetName))
                ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) & PHRPReader.ReaderFactory.GetPHRPSeqInfoFileName(PeptideHitResultTypes, strDatasetName))
                ShowMessage("SeqInfo file: ".PadRight(PREVIEW_PAD_WIDTH) & PHRPReader.ReaderFactory.GetPHRPSeqToProteinMapFileName(PeptideHitResultTypes, strDatasetName))
            End If
        End If

        If blnLoadMSGFResults Then
            ShowMessage("MSGF Results file: ".PadRight(PREVIEW_PAD_WIDTH) & PHRPReader.ReaderFactory.GetMSGFFileName(strInputFilePath))
        End If

        If blnLoadScanStats Then
            ShowMessage("ScanStats file: ".PadRight(PREVIEW_PAD_WIDTH) & PHRPReader.ReaderFactory.GetScanStatsFilename(mDatasetName))
            ShowMessage("ScanStats file: ".PadRight(PREVIEW_PAD_WIDTH) & PHRPReader.ReaderFactory.GetExtendedScanStatsFilename(mDatasetName))
        End If

        If Not String.IsNullOrEmpty(strSearchEngineParamFileName) Then
            ShowMessage("Search Engine Params: ".PadRight(PREVIEW_PAD_WIDTH) & strSearchEngineParamFileName)
            ShowMessage("Tool Version File: ".PadRight(PREVIEW_PAD_WIDTH) & PHRPReader.ReaderFactory.GetToolVersionInfoFilenames(PeptideHitResultTypes).First())

            If mPeptideHitResultType = PHRPReader.PeptideHitResultTypes.XTandem Then
                ' Determine the additional files that will be required
                Dim lstFileNames As List(Of String)
                lstFileNames = PHRPReader.Reader.XTandemSynFileReader.GetAdditionalSearchEngineParamFileNames(Path.Combine(fiFileInfo.DirectoryName, strSearchEngineParamFileName))

                For Each strFileName In lstFileNames
                    ShowMessage("Search Engine Params: ".PadRight(PREVIEW_PAD_WIDTH) & strFileName)
                Next

            End If
        End If

    End Sub

    ' ReSharper disable once UnusedMember.Global
    ''' <summary>
    ''' Main processing function; calls ConvertPHRPDataToXML
    ''' </summary>
    ''' <param name="strInputFilePath">PHRP Input file path</param>
    ''' <param name="strOutputFolderPath">Output folder path (if empty, then output file will be created in the same folder as the input file)</param>
    ''' <param name="strParameterFilePath">Parameter file path</param>
    ''' <param name="blnResetErrorCode">True to reset the error code prior to processing</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Overloads Overrides Function ProcessFile(strInputFilePath As String, strOutputFolderPath As String, strParameterFilePath As String, blnResetErrorCode As Boolean) As Boolean
        ' Returns True if success, False if failure

        Dim ioFile As FileInfo
        Dim strInputFilePathFull As String

        Dim blnSuccess As Boolean

        If blnResetErrorCode Then
            SetLocalErrorCode(PeptideListToXMLErrorCodes.NoError)
        End If

        If Not LoadParameterFileSettings(strParameterFilePath) Then
            ShowErrorMessage("Parameter file load error: " & strParameterFilePath)

            If MyBase.ErrorCode = ProcessFilesErrorCodes.NoError Then
                MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidParameterFile)
            End If
            Return False
        End If

        Try
            If strInputFilePath Is Nothing OrElse strInputFilePath.Length = 0 Then
                ShowMessage("Input file name is empty")
                MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.InvalidInputFilePath)
            Else

                Console.WriteLine()
                If Not mPreviewMode Then
                    ShowMessage("Parsing " & Path.GetFileName(strInputFilePath))
                End If

                ' Note that CleanupFilePaths() will update mOutputFolderPath, which is used by LogMessage()
                If Not CleanupFilePaths(strInputFilePath, strOutputFolderPath) Then
                    MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.FilePathError)
                Else

                    If Not mPreviewMode Then
                        MyBase.ResetProgress()
                    End If

                    Try
                        ' Obtain the full path to the input file
                        ioFile = New FileInfo(strInputFilePath)
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

    Private Sub SetLocalErrorCode(newErrorCode As PeptideListToXMLErrorCodes)
        SetLocalErrorCode(newErrorCode, False)
    End Sub

    Private Sub SetLocalErrorCode(newErrorCode As PeptideListToXMLErrorCodes, blnLeaveExistingErrorCodeUnchanged As Boolean)

        If blnLeaveExistingErrorCodeUnchanged AndAlso mLocalErrorCode <> PeptideListToXMLErrorCodes.NoError Then
            ' An error code is already defined; do not change it
        Else
            mLocalErrorCode = newErrorCode

            If newErrorCode = PeptideListToXMLErrorCodes.NoError Then
                If MyBase.ErrorCode = ProcessFilesErrorCodes.LocalizedError Then
                    MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.NoError)
                End If
            Else
                MyBase.SetBaseClassErrorCode(ProcessFilesErrorCodes.LocalizedError)
            End If
        End If

    End Sub

    Protected Function WriteCachedData(strInputFilePath As String, strOutputFilePath As String, ByRef objSearchEngineParams As PHRPReader.Data.SearchEngineParameters) As Boolean

        Dim intSpectra As Integer
        Dim intPeptides As Integer

        Dim blnSuccess As Boolean

        Dim strSpectrumKey As String
        Dim udtCurrentSpectrum = New clsPepXMLWriter.udtSpectrumInfoType

        MyBase.ResetProgress("Creating the .pepXML file")

        Try
            Console.WriteLine()
            ShowMessage("Creating PepXML file at " & Path.GetFileName(strOutputFilePath))

            mXMLWriter = New clsPepXMLWriter(mDatasetName, mFastaFilePath, objSearchEngineParams, strInputFilePath, strOutputFilePath)
            RegisterEvents(mXMLWriter)

            If Not mXMLWriter.IsWritable Then
                ShowErrorMessage("XMLWriter is not writable; aborting")
                Return False
            End If

            mXMLWriter.MaxProteinsPerPSM = mMaxProteinsPerPSM

            intSpectra = 0
            intPeptides = 0
            For Each objItem In mPSMsBySpectrumKey

                strSpectrumKey = objItem.Key
                If mSpectrumInfo.TryGetValue(strSpectrumKey, udtCurrentSpectrum) Then
                    intSpectra += 1
                    intPeptides += objItem.Value.Count

                    mXMLWriter.WriteSpectrum(udtCurrentSpectrum, objItem.Value, mSeqToProteinMapCached)
                Else
                    ShowErrorMessage("Spectrum key '" & strSpectrumKey & "' not found in mSpectrumInfo; this is unexpected")
                End If

                Dim pctComplete As Single = intSpectra / CSng(mPSMsBySpectrumKey.Count) * 100
                UpdateProgress(pctComplete)

            Next

            If intPeptides > 500 Then
                Console.WriteLine()
            End If

            mXMLWriter.CloseDocument()

            Console.WriteLine()
            ShowMessage("PepXML file created with " & intSpectra.ToString("#,##0") & " spectra and " & intPeptides.ToString("#,##0") & " peptides")

            blnSuccess = True

        Catch ex As Exception
            ShowErrorMessage("Error Reading source file in WriteCachedData: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess
    End Function

End Class