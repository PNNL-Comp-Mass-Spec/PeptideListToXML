#
# Originally from http://code.google.com/p/massspec-toolbox/source/browse/trunk/search/sequest-pepxml2hit_list.py
# Modified by Matthew Monroe in September 2012 to extract additional columns
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
            if( not self.PSM.has_key(self.spectrum_id) ):
                self.PSM[self.spectrum_id] = dict()
                self.PSM[self.spectrum_id]['search_hit'] = []
            else:
                print "Duplicate PSM : %s"%self.spectrum_id
            self.PSM[self.spectrum_id]['charge'] = int(attr['assumed_charge'])
            self.PSM[self.spectrum_id]['neutral_mass'] = float(attr['precursor_neutral_mass'])
        if( len(self.element_array) == 5 and name == 'search_hit' ):
            self.is_search_hit = True
            self.search_hit = dict()
            self.search_hit['hit_rank'] = int(attr['hit_rank'])
            self.search_hit['peptide'] = attr['peptide']
            self.search_hit['protein'] = attr['protein']
            self.search_hit['missed_cleavages'] = int(attr['num_missed_cleavages'])
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

            ## X!Tandem
            if(attr['name'] == 'hyperscore'):
                self.search_hit['hyperscore'] = float(attr['value'])
            if(attr['name'] == 'expect'):
                self.search_hit['expect'] = float(attr['value'])
            ## InsPecT
            if(attr['name'] == 'mqscore'):
                self.search_hit['mqscore'] = float(attr['value'])
            if(attr['name'] == 'expect'):
                self.search_hit['expect'] = float(attr['value'])
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
            if(attr['name'] == 'start_scan'):
                self.search_hit['start_scan'] = int(attr['value'])
            if(attr['name'] == 'end_scan'):
                self.search_hit['end_scan'] = int(attr['value'])
            if(attr['name'] == 'retention_time_sec'):
                self.search_hit['retention_time_sec'] = float(attr['value'])
            if(attr['name'] == 'NumTrypticEnds'):
                self.search_hit['NumTrypticEnds'] = int(attr['value'])
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
