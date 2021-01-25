﻿Option Strict On

Imports System.IO
Imports System.Reflection
Imports System.Threading
Imports PRISM
' This program reads a tab-delimited text file of peptide sequence and
' creates a PepXML or mzIdentML file with the appropriate information

' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Program started April 13, 2012

' E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
' Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/
' -------------------------------------------------------------------------------
'
' Licensed under the Apache License, Version 2.0; you may not use this file except
' in compliance with the License.  You may obtain a copy of the License at
' http://www.apache.org/licenses/LICENSE-2.0

Module modMain

    ' Ignore Spelling: mzIdentML

    Public Const PROGRAM_DATE As String = "January 25, 2021"

    Private mInputFilePath As String
    Private mOutputFolderPath As String             ' Optional
    Private mParameterFilePath As String            ' Optional

    Private mFastaFilePath As String
    Private mSearchEngineParamFileName As String
    Private mHitsPerSpectrum As Integer              ' Number of hits per spectrum to store; 0 means to store all hits
    Private mPreview As Boolean

    Private mSkipXPeptides As Boolean
    Private mTopHitOnly As Boolean
    Private mMaxProteinsPerPSM As Integer

    Private mPeptideFilterFilePath As String
    Private mChargeFilterList As List(Of Integer)

    Private mLoadModsAndSeqInfo As Boolean
    Private mLoadMSGFResults As Boolean
    Private mLoadScanStats As Boolean

    ' Future enum; mzIdentML is not yet supported
    ' Private mOutputFormat As clsPeptideListToXML.PeptideListOutputFormat

    Private mOutputFolderAlternatePath As String                ' Optional
    Private mRecreateFolderHierarchyInAlternatePath As Boolean  ' Optional

    Private mRecurseFolders As Boolean
    Private mRecurseFoldersMaxLevels As Integer

    Private mLogMessagesToFile As Boolean
    ' Unused: Private mLogFilePath As String = String.Empty
    ' Unused: Private mLogFolderPath As String = String.Empty

    Private mPeptideListConverter As clsPeptideListToXML
    Private mLastProgressReportTime As DateTime
    Private mLastPercentDisplayed As DateTime

    ''' <summary>
    ''' Program entry point
    ''' </summary>
    ''' <returns>0 if no error, error code if an error</returns>
    ''' <remarks></remarks>
    Public Function Main() As Integer

        Dim intReturnCode As Integer
        Dim commandLineParser As New clsParseCommandLine
        Dim blnProceed As Boolean

        ' Initialize the options
        mInputFilePath = String.Empty
        mOutputFolderPath = String.Empty
        mParameterFilePath = String.Empty

        mFastaFilePath = String.Empty
        mSearchEngineParamFileName = String.Empty
        mHitsPerSpectrum = 3
        mPreview = False

        mSkipXPeptides = False
        mTopHitOnly = False
        mMaxProteinsPerPSM = 100

        mPeptideFilterFilePath = String.Empty
        mChargeFilterList = New List(Of Integer)

        mLoadModsAndSeqInfo = True
        mLoadMSGFResults = True
        mLoadScanStats = True

        ' Future enum; mzIdentML is not yet supported
        ' mOutputFormat = clsPeptideListToXML.PeptideListOutputFormat.PepXML

        mRecurseFolders = False
        mRecurseFoldersMaxLevels = 0

        mLogMessagesToFile = False
        ' Unused: mLogFilePath = String.Empty
        ' Unused: mLogFolderPath = String.Empty

        Try
            blnProceed = False
            If commandLineParser.ParseCommandLine Then
                If SetOptionsUsingCommandLineParameters(commandLineParser) Then blnProceed = True
            End If

            If Not blnProceed OrElse
               commandLineParser.NeedToShowHelp OrElse
               commandLineParser.ParameterCount + commandLineParser.NonSwitchParameterCount = 0 OrElse
               mInputFilePath.Length = 0 Then
                ShowProgramHelp()
                intReturnCode = -1
            Else
                ' Note: the following settings will be overridden if mParameterFilePath points to a valid parameter file that has these settings defined

                mPeptideListConverter = New clsPeptideListToXML() With {
                    .LogMessagesToFile = mLogMessagesToFile,
                    .FastaFilePath = mFastaFilePath,
                    .SearchEngineParamFileName = mSearchEngineParamFileName,
                    .HitsPerSpectrum = mHitsPerSpectrum,
                    .PreviewMode = mPreview,
                    .SkipXPeptides = mSkipXPeptides,
                    .TopHitOnly = mTopHitOnly,
                    .MaxProteinsPerPSM = mMaxProteinsPerPSM,
                    .PeptideFilterFilePath = mPeptideFilterFilePath,
                    .ChargeFilterList = mChargeFilterList,
                    .LoadModsAndSeqInfo = mLoadModsAndSeqInfo,
                    .LoadMSGFResults = mLoadMSGFResults,
                    .LoadScanStats = mLoadScanStats
                }

                RegisterEvents(mPeptideListConverter)
                mLastProgressReportTime = DateTime.UtcNow
                mLastPercentDisplayed = DateTime.UtcNow

                If mRecurseFolders Then
                    If mPeptideListConverter.ProcessFilesAndRecurseDirectories(mInputFilePath, mOutputFolderPath, mOutputFolderAlternatePath, mRecreateFolderHierarchyInAlternatePath, mParameterFilePath, mRecurseFoldersMaxLevels) Then
                        intReturnCode = 0
                    Else
                        intReturnCode = mPeptideListConverter.ErrorCode
                    End If
                Else
                    If mPeptideListConverter.ProcessFilesWildcard(mInputFilePath, mOutputFolderPath, mParameterFilePath) Then
                        intReturnCode = 0
                    Else
                        intReturnCode = mPeptideListConverter.ErrorCode
                        If intReturnCode <> 0 Then
                            ShowErrorMessage("Error while processing: " & mPeptideListConverter.GetErrorMessage())
                        End If
                    End If
                End If

            End If

        Catch ex As Exception
            ShowErrorMessage("Error occurred in modMain->Main", ex)
            intReturnCode = -1
        End Try

        Return intReturnCode

    End Function

    Private Sub DisplayProgressPercent(taskDescription As String, intPercentComplete As Integer, blnAddCarriageReturn As Boolean)
        If blnAddCarriageReturn Then
            Console.WriteLine()
        End If
        If intPercentComplete > 100 Then intPercentComplete = 100
        If String.IsNullOrEmpty(taskDescription) Then taskDescription = "Processing"

        Console.Write(taskDescription & ": " & intPercentComplete.ToString() & "% ")
        If blnAddCarriageReturn Then
            Console.WriteLine()
        End If
    End Sub

    Private Function GetAppVersion() As String
        Return Assembly.GetExecutingAssembly().GetName().Version.ToString() & " (" & PROGRAM_DATE & ")"
    End Function

    Private Function SetOptionsUsingCommandLineParameters(commandLineParser As clsParseCommandLine) As Boolean
        ' Returns True if no problems; otherwise, returns false

        Dim strValue As String = String.Empty
        Dim lstValidParameters = New List(Of String) From {
                "I", "O", "F", "E", "H", "X",
                "PepFilter", "ChargeFilter", "TopHitOnly",
                "MaxProteins", "NoMods", "NoMSGF", "NoScanStats",
                "Preview", "P", "S", "A", "R", "L"}
        Dim intValue As Integer

        Try
            ' Make sure no invalid parameters are present
            If commandLineParser.InvalidParametersPresent(lstValidParameters) Then
                ShowErrorMessage("Invalid command line parameters",
                  (From item In commandLineParser.InvalidParameters(lstValidParameters) Select "/" + item).ToList())
                Return False
            Else
                With commandLineParser
                    ' Query commandLineParser to see if various parameters are present
                    If .RetrieveValueForParameter("I", strValue) Then
                        mInputFilePath = strValue
                    ElseIf .NonSwitchParameterCount > 0 Then
                        mInputFilePath = .RetrieveNonSwitchParameter(0)
                    End If

                    If .RetrieveValueForParameter("O", strValue) Then mOutputFolderPath = strValue

                    ' Future enum; mzIdentML is not yet supported
                    'If .RetrieveValueForParameter("M", strValue) Then
                    '    mOutputFormat = clsPeptideListToXML.PeptideListOutputFormat.mzIdentML
                    'End If

                    If .RetrieveValueForParameter("F", strValue) Then mFastaFilePath = strValue

                    If .RetrieveValueForParameter("E", strValue) Then mSearchEngineParamFileName = strValue

                    If .RetrieveValueForParameter("H", strValue) Then
                        If Integer.TryParse(strValue, intValue) Then
                            mHitsPerSpectrum = intValue
                        End If
                    End If

                    If .IsParameterPresent("X") Then mSkipXPeptides = True
                    If .IsParameterPresent("TopHitOnly") Then mTopHitOnly = True

                    If .RetrieveValueForParameter("MaxProteins", strValue) Then
                        If Integer.TryParse(strValue, intValue) Then
                            mMaxProteinsPerPSM = intValue
                        End If
                    End If

                    If .RetrieveValueForParameter("PepFilter", strValue) Then mPeptideFilterFilePath = strValue
                    If .RetrieveValueForParameter("ChargeFilter", strValue) Then
                        Try
                            If String.IsNullOrEmpty(strValue) Then
                                ShowErrorMessage("ChargeFilter switch must have one or more charges, for example /ChargeFilter:2  or /ChargeFilter:2,3")
                                Console.WriteLine()
                                Return False
                            Else
                                For Each strCharge As String In strValue.Split(","c).ToList()
                                    Dim intCharge As Integer
                                    If Integer.TryParse(strCharge, intCharge) Then
                                        mChargeFilterList.Add(intCharge)
                                    Else
                                        ShowErrorMessage("Invalid charge specified: " & strCharge)
                                        Console.WriteLine()
                                        Return False
                                    End If
                                Next
                            End If
                        Catch ex As Exception
                            ShowErrorMessage("Error parsing the list of charges """ & strValue & """; should be a command separated list")
                            Console.WriteLine()
                            Return False
                        End Try
                    End If

                    If .RetrieveValueForParameter("P", strValue) Then mParameterFilePath = strValue

                    If .IsParameterPresent("NoMods") Then mLoadModsAndSeqInfo = False
                    If .IsParameterPresent("NoMSGF") Then mLoadMSGFResults = False
                    If .IsParameterPresent("NoScanStats") Then mLoadScanStats = False

                    If .IsParameterPresent("Preview") Then mPreview = True

                    If .RetrieveValueForParameter("S", strValue) Then
                        mRecurseFolders = True
                        If Not Integer.TryParse(strValue, mRecurseFoldersMaxLevels) Then
                            mRecurseFoldersMaxLevels = 0
                        End If
                    End If

                    If .RetrieveValueForParameter("A", strValue) Then mOutputFolderAlternatePath = strValue
                    If .IsParameterPresent("R") Then mRecreateFolderHierarchyInAlternatePath = True

                    If .IsParameterPresent("L") Then mLogMessagesToFile = True

                End With

                Return True
            End If

        Catch ex As Exception
            ShowErrorMessage("Error parsing the command line parameters", ex)
        End Try

        Return False

    End Function

    Private Sub ShowErrorMessage(errorMessage As String, Optional ex As Exception = Nothing)
        ConsoleMsgUtils.ShowError(errorMessage, ex)
    End Sub

    Private Sub ShowErrorMessage(title As String, errorMessages As List(Of String))
        ConsoleMsgUtils.ShowErrors(title, errorMessages)
    End Sub

    Private Sub ShowProgramHelp()

        Try
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                "This program reads a tab-delimited text file created by the Peptide Hit Results Processor (PHRP) and " &
                "creates a PepXML with the appropriate information.  The various _SeqInfo files created by PHRP must be present in the same folder as the text file. " &
                "If the MASIC Scan Stats file is also present, then elution time information will be extracted and included in the PepXML file. " &
                "You should ideally also include the name of the parameter file used for the MS/MS search engine."))

            Console.WriteLine()

            Console.WriteLine("Program syntax:")
            Console.WriteLine(Path.GetFileName(Assembly.GetExecutingAssembly().Location) & " /I:PHRPResultsFile [/O:OutputFolderPath]")
            Console.WriteLine(" [/E:SearchEngineParamFileName] [/F:FastaFilePath] [/P:ParameterFilePath]")
            Console.WriteLine(" [/H:HitsPerSpectrum] [/X] [/TopHitOnly] [/MaxProteins:" & clsPeptideListToXML.DEFAULT_MAX_PROTEINS_PER_PSM & "]")
            Console.WriteLine(" [/PepFilter:PeptideFilterFilePath] [/ChargeFilter:ChargeList]")
            Console.WriteLine(" [/NoMods] [/NoMSGF] [/NoScanStats] [/Preview]")
            Console.WriteLine(" [/S:[MaxLevel]] [/A:AlternateOutputFolderPath] [/R] [/L] [/Q]")
            Console.WriteLine()
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                "The input file path can contain the wildcard character * and should point to a tab-delimited text file created by PHRP (for example, Dataset_syn.txt, Dataset_xt.txt, Dataset_msgfplus_syn.txt or Dataset_inspect_syn.txt) " &
                "The output folder switch is optional.  If omitted, the output file will be created in the same folder as the input file. "))
            Console.WriteLine()
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /E to specify the name of the parameter file used by the MS/MS search engine (must be in the same folder as the PHRP results file).  For X!Tandem results, the default_input.xml and taxonomy.xml files must also be present in the input folder."))
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /F to specify the path to the fasta file to store in the PepXML file; ignored if /E is provided and the search engine parameter file defines the fasta file to search (this is the case for SEQUEST and X!Tandem but not Inspect or MSGF+)"))
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /H to specify the number of matches per spectrum to store (default is " & clsPeptideListToXML.DEFAULT_HITS_PER_SPECTRUM & "; use 0 to keep all hits)"))
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /X to specify that peptides with X residues should be skipped"))
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /TopHitOnly to specify that each scan should only include a single peptide match (regardless of charge)"))
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /MaxProteins to define the maximum number of proteins to track for each PSM (default is " & clsPeptideListToXML.DEFAULT_MAX_PROTEINS_PER_PSM & ")"))
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /PepFilter:File to use a text file to filter the peptides included in the output file (one peptide sequence per line)"))
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /ChargeFilter:ChargeList to specify one or more charges to filter on. Examples:"))
            Console.WriteLine("  Only 2+ peptides:    /ChargeFilter:2")
            Console.WriteLine("  2+ and 3+ peptides:  /ChargeFilter:2,3")
            Console.WriteLine()
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("By default, the _ModSummary file and SeqInfo files are loaded and used to determine the modified residues; use /NoMods to skip these files"))
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("By default, the _MSGF.txt file is loaded to associated MSGF SpecProb values with the results; use /NoMSGF to skip this file"))
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("By default, the MASIC _ScanStats.txt and _ScanStatsEx.txt files are loaded to determine elution times for scan numbers; use /NoScanStats to skip these files"))
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /Preview to preview the files that would be required for the specified dataset (taking into account the other command line switches used)"))
            Console.WriteLine()
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /P to specific a parameter file to use.  Options in this file will override options specified for /E, /F, /H, and /X"))
            Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                "Use /S to process all valid files in the input directory and subdirectories. Include a number after /S (like /S:2) to limit the level of subdirectories to examine. " &
                "When using /S, you can redirect the output of the results using /A. " &
                "When using /S, you can use /R to re-create the input folder hierarchy in the alternate output folder (if defined)."))

            Console.WriteLine(ConsoleMsgUtils.WrapParagraph("Use /L to log messages to a file.  If /Q is used, then no messages will be displayed at the console."))
            Console.WriteLine()

            Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2012")
            Console.WriteLine("Version: " & GetAppVersion())
            Console.WriteLine()

            Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov")
            Console.WriteLine("Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/")
            Console.WriteLine()

            ' Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
            Thread.Sleep(750)

        Catch ex As Exception
            ShowErrorMessage("Error displaying the program syntax", ex)
        End Try

    End Sub

    Private Sub RegisterEvents(processingClass As IEventNotifier)

        AddHandler processingClass.DebugEvent, AddressOf ProcessingClass_DebugEvent
        AddHandler processingClass.ErrorEvent, AddressOf ProcessingClass_ErrorEvent
        AddHandler processingClass.StatusEvent, AddressOf ProcessingClass_StatusEvent
        AddHandler processingClass.WarningEvent, AddressOf ProcessingClass_WarningEvent
        AddHandler processingClass.ProgressUpdate, AddressOf ProcessingClass_ProgressUpdate

    End Sub

    Private Sub ProcessingClass_DebugEvent(message As String)
        ConsoleMsgUtils.ShowDebug(message)
    End Sub

    Private Sub ProcessingClass_ErrorEvent(message As String, ex As Exception)
        ConsoleMsgUtils.ShowError(message, ex)
    End Sub

    Private Sub ProcessingClass_StatusEvent(message As String)
        Console.WriteLine(message)
    End Sub

    Private Sub ProcessingClass_WarningEvent(message As String)
        ConsoleMsgUtils.ShowWarning(message)
    End Sub

    Private Sub ProcessingClass_ProgressUpdate(progressMessage As String, percentComplete As Single)
        Const PROGRESS_DOT_INTERVAL_MSEC = 250

        If DateTime.UtcNow.Subtract(mLastPercentDisplayed).TotalSeconds >= 15 Then
            Console.WriteLine()

            DisplayProgressPercent(progressMessage, CInt(percentComplete), False)
            mLastPercentDisplayed = DateTime.UtcNow
        Else
            If DateTime.UtcNow.Subtract(mLastProgressReportTime).TotalMilliseconds > PROGRESS_DOT_INTERVAL_MSEC Then
                mLastProgressReportTime = DateTime.UtcNow
                Console.Write(".")
            End If
        End If
    End Sub

End Module
