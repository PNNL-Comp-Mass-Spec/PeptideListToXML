#
# Originally from http://code.google.com/p/massspec-toolbox/source/browse/trunk/search/sequest-pepxml2hit_list.py
# Modified by Matthew Monroe in September 2012 to extract additional columns
#
# This library file is used by pepxml2hit_list.py
#
# 2018-06-06 mem - Update to Python 3.x and update to support .pepXML files from MSFragger and MSGF+
#

import xml.sax
from decimal import Decimal, getcontext

class pepxml_parser(xml.sax.ContentHandler):
    element_array = []
    is_spectrum_query = False
    is_search_hit = False
    PSM = dict()
    search_hit = dict()
    spectrum_id = ''

    getcontext().prec = 32

    def startElement(self,name,attr):
        self.element_array.append(name)
        if( len(self.element_array) == 3 and name == 'spectrum_query' ):
            self.is_spectrum_query = True
            self.spectrum_id = attr['spectrum']
            if( self.spectrum_id not in self.PSM ):
                self.PSM[self.spectrum_id] = dict()
                self.PSM[self.spectrum_id]['search_hit'] = []
            else:
                print("Duplicate PSM : %s"%self.spectrum_id)
                
            self.PSM[self.spectrum_id]['charge'] = int(attr['assumed_charge'])
            self.PSM[self.spectrum_id]['neutral_mass'] = float(attr['precursor_neutral_mass'])

            self.PSM[self.spectrum_id]['start_scan'] = int(attr['start_scan'])
            self.PSM[self.spectrum_id]['end_scan'] = int(attr['end_scan'])
            self.PSM[self.spectrum_id]['retention_time_sec'] = float(attr['retention_time_sec'])

        if( len(self.element_array) == 5 and name == 'search_hit' ):
            self.is_search_hit = True
            self.search_hit = dict()
            self.search_hit['hit_rank'] = int(attr['hit_rank'])
            self.search_hit['peptide'] = attr['peptide']
            self.search_hit['protein'] = attr['protein']
            self.search_hit['missed_cleavages'] = int(attr['num_missed_cleavages'])
            self.search_hit['NumTrypticEnds'] = int(attr['num_tol_term'])
            self.search_hit['Ions_Matched'] = int(attr['num_matched_ions'])
            self.search_hit['Ions_Observed'] = int(attr['tot_num_ions'])

        if( len(self.element_array) == 6 and name == 'search_score' ):
            ## SEQUEST
            if(attr['name'] == 'xcorr'):
                self.search_hit['xcorr'] = float(attr['value'])
            if(attr['name'] == 'spscore'):
                self.search_hit['spscore'] = float(attr['value'])
            if(attr['name'] == 'deltacn'):
                self.search_hit['deltacn'] = float(attr['value'])
            if(attr['name'] == 'deltacnstar'):
                self.search_hit['deltacnstar'] = float(attr['value'])
            if(attr['name'] == 'RankXc'):
                self.search_hit['RankXc'] = int(attr['value'])
            if(attr['name'] == 'XcRatio'):
                self.search_hit['XcRatio'] = float(attr['value'])
            if(attr['name'] == 'Ions_Observed'):
                self.search_hit['Ions_Observed'] = int(attr['value'])
            if(attr['name'] == 'Ions_Expected'):
                self.search_hit['Ions_Expected'] = int(attr['value'])

            ## X!Tandem and MSFragger
            if(attr['name'] == 'hyperscore'):
                self.search_hit['hyperscore'] = float(attr['value'])
            if(attr['name'] == 'nextscore'):
                self.search_hit['nextscore'] = float(attr['value'])
            if(attr['name'] == 'expect'):
                self.search_hit['expect'] = float(attr['value'])
                
            ## InsPecT
            if(attr['name'] == 'mqscore'):
                self.search_hit['mqscore'] = float(attr['value'])
            # InsPecT reports expect values; already handled above
            if(attr['name'] == 'fscore'):
                self.search_hit['fscore'] = float(attr['value'])
            if(attr['name'] == 'deltascore'):
                self.search_hit['deltascore'] = float(attr['value'])

            ## MyriMatch
            if(attr['name'] == 'mvh'):
                self.search_hit['mvh'] = float(attr['value'])
            if(attr['name'] == 'massError'):
                self.search_hit['massError'] = float(attr['value'])
            if(attr['name'] == 'mzSSE'):
                self.search_hit['mzSSE'] = float(attr['value'])
            if(attr['name'] == 'mzFidelity'):
                self.search_hit['mzFidelity'] = float(attr['value'])
            if(attr['name'] == 'newMZFidelity'):
                self.search_hit['newMZFidelity'] = float(attr['value'])
            if(attr['name'] == 'mzMAE'):
                self.search_hit['mzMAE'] = float(attr['value'])

            ## DirecTag-TagRecon
            if(attr['name'] == 'numPTMs'):
                self.search_hit['numPTMs'] = int(attr['value'])

            ## Generic additional
            if(attr['name'] == 'NumTrypticEnds'):
                self.search_hit['NumTrypticEnds'] = int(attr['value'])

            ## MSGF+
            # Track MSGF+ EValues using 'expect' aka expectation value
            if(attr['name'] == 'EValue'):
                self.search_hit['expect'] = float(attr['value'])

            if(attr['name'] == 'msgfspecprob'):
                if(attr['value'] != ''):
                    self.search_hit['msgfspecprob'] = Decimal(attr['value'])
       
        ## PeptideProphet
        if( len(self.element_array) == 7 and name == 'peptideprophet_result' ):
            self.search_hit['TPP_pep_prob'] = float(attr['probability'])

    def endElement(self,name):
        if( len(self.element_array) == 3 and name == 'spectrum_query' ):
            self.spectrum_id = ''
            self.is_spectrum_query = False
        if( len(self.element_array) == 5 and name == 'search_hit' ):
            self.PSM[self.spectrum_id]['search_hit'].append(self.search_hit)
            self.search_hit = dict()
            self.is_search_hit = False
        self.element_array.pop()
    
def parse_by_filename(filename_pepxml):
    p = pepxml_parser()
    xml.sax.parse(filename_pepxml,p)
    return p.PSM
