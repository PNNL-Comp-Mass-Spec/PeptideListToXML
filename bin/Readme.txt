The PeptideListToXML application reads a tab-delimited text file created 
by the Peptide Hit Results Processor (PHRP) and creates a PepXML with 
the appropriate information.  The various _SeqInfo files created by PHRP 
must be present in the same folder as the text file. If the MASIC Scan Stats 
file is also present, then elution time information will be extracted and 
included in the PepXML file.  You should ideally also include the name 
of the parameter file used for the MS/MS search engine.

Program syntax:
PeptideListToXML.exe /I:PHRPResultsFile [/O:OutputFolderPath]
 [/E:SearchEngineParamFileName] [/F:FastaFilePath] [/H:HitsPerSpectrum] [/X] [/P:ParameterFilePath]
 [/NoMods] [/NoMSGF] [/NoScanStats] [/NoSeqInfo] [/Preview]
 [/S:[MaxLevel]] [/A:AlternateOutputFolderPath] [/R] [/L] [/Q]

The input file path can contain the wildcard character * and should point to 
a tab-delimited text file created by PHRP (for example, Dataset_syn.txt, 
Dataset_xt.txt, Dataset_msgfdb_syn.txt or Dataset_inspect_syn.txt) The output 
folder switch is optional.  If omitted, the output file will be created in 
the same folder as the input file.

Use /E to specify the name of the parameter file used by the MS/MS 
search engine (must be in the same folder as the PHRP results file).  
For X!Tandem results, the default_input.xml and taxonomy.xml files must 
also be present in the input folder.

Use /F to specify the path to the fasta file to store in the PepXML file; 
ignored if /E is provided and the search engine parameter file defines the 
fasta file to search (this is the case for Sequest and X!Tandem but not Inspect or MSGFDB).

Use /H to specify the number of matches per spectrum to store (default is 3; use 0 to keep all hits)

Use /X to specify that peptides with X residues should be skipped

By default, the _ModSummary file and SeqInfo files are loaded and used 
to determine the modified residues; use /NoMods to skip these files

By default, the _MSGF.txt file is loaded to associated MSGF SpecProb 
values with the results; use /NoMSGF to skip this file

By default, the MASIC _ScanStats.txt and _ScanStatsEx.txt files are loaded 
to determine elution times for scan numbers; use /NoScanStats to skip these files

Use /Preview to preview the files that would be required for the 
specified dataset (taking into account the other command line switches used)

Use /P to specific a parameter file to use.  Options in this file will 
override options specified for /E, /F, /H, and /X

Use /S to process all valid files in the input folder and subfolders. 
Include a number after /S (like /S:2) to limit the level of subfolders to examine. 

When using /S, you can redirect the output of the results using /A. 

When using /S, you can use /R to re-create the input folder hierarchy in 
the alternate output folder (if defined).

Use /L to log messages to a file.  If /Q is used, then no messages 
will be displayed at the console.

-------------------------------------------------------------------------------
Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2012
Version: 1.0.4502.17446 (April 27, 2012)

E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
Website: http://ncrr.pnl.gov/ or http://omics.pnl.gov
-------------------------------------------------------------------------------

Licensed under the Apache License, Version 2.0; you may not use this file except 
in compliance with the License.  You may obtain a copy of the License at 
http://www.apache.org/licenses/LICENSE-2.0
