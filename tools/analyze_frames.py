"""Report observed frametimes. Low = reciprocal of mean slowest fraction, not percentile FPS."""
import csv,json,math,statistics,sys
from pathlib import Path
def analyze(path):
 with Path(path).open() as source:rows=list(csv.DictReader(source))
 frames=[float(r['frametime_ms']) for r in rows if math.isfinite(float(r['frametime_ms'])) and float(r['frametime_ms'])>0]
 if not frames:raise ValueError('No valid frametimes')
 slow=sorted(frames,reverse=True);mean=statistics.mean(frames)
 low=lambda fraction:1000/statistics.mean(slow[:max(1,math.ceil(len(slow)*fraction))])
 return {'samples':len(frames),'average_fps':1000/mean,'one_percent_low':low(.01),'point_one_percent_low':low(.001),'mean_frametime_ms':mean,'frametime_variance_ms2':statistics.pvariance(frames),'max_frametime_ms':max(frames),'spikes_over_50ms':sum(x>50 for x in frames),'max_heap_mib':max(int(r.get('heap_bytes',0)) for r in rows)/1048576,'vram':'UNMEASURED','input_to_photon':'UNMEASURED','allocation_rate':'UNMEASURED','gc_pause':'UNMEASURED'}
if __name__=='__main__':print(json.dumps(analyze(sys.argv[1]),indent=2))
