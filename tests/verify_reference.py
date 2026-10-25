"""Independent validation only. SciPy is not a runtime dependency of the app."""
import csv, sys, pathlib, re, json
import numpy as np
import scipy
from scipy import signal
from scipy.signal import windows

root = pathlib.Path(__file__).resolve().parents[1]
reference = pathlib.Path(sys.argv[1])
mapping = dict(Rectangular=windows.boxcar, Hann=windows.hann,
    Hamming=windows.hamming, Blackman=windows.blackman,
    BlackmanHarris=windows.blackmanharris, Nuttall=windows.nuttall,
    Bartlett=windows.bartlett, BartlettHann=windows.barthann,
    FlatTop=windows.flattop, Kaiser=windows.kaiser,
    Tukey=windows.tukey, Gaussian=windows.gaussian)
groups = {}
with reference.open() as f:
    for row in csv.DictReader(f):
        key = row['kind'], int(row['n']), row['periodic'] == 'True', float(row['parameter'])
        groups.setdefault(key, []).append(float(row['value']))
worst = 0.0
worst_case = None
samples = 0
for (kind, n, periodic, parameter), actual in groups.items():
    args = [n]
    if kind in ('Kaiser', 'Tukey', 'Gaussian'):
        args.append(parameter)
    expected = mapping[kind](*args, sym=not periodic)
    error = float(np.max(np.abs(expected-np.array(actual))))
    if error > worst:
        worst, worst_case = error, [kind, n, periodic, parameter]
    np.testing.assert_allclose(actual, expected, atol=3e-14, rtol=3e-14,
        err_msg=str((kind, n, periodic, parameter)))
    samples += n

def coe(path):
    text = re.sub(r';[^\r\n]*', ';', path.read_text(encoding='ascii'))
    # Comment lines begin with semicolon; keyword terminators remain.
    text = '\n'.join(line for line in text.splitlines() if not line.strip().startswith(';'))
    match = re.fullmatch(r'\s*memory_initialization_radix\s*=\s*(2|10|16)\s*;\s*'
        r'memory_initialization_vector\s*=\s*([\da-fA-F,\s]+)\s*;\s*', text)
    assert match, path
    radix = int(match[1])
    tokens = match[2].split(',')
    return np.array([int(t.strip(), radix) for t in tokens], dtype=np.uint64)

results = root/'tests'/'results'
for path in results.glob('Vivado_W*_R*.coe'):
    width, radix = map(int, re.search(r'_W(\d+)_R(\d+)', path.name).groups())
    x = windows.hann(1024, sym=True)*(0 if width == 1 else 1-2.**(-(width-1)))
    expected = np.rint(x*2.**(width-1)).astype(np.uint64)
    np.testing.assert_array_equal(coe(path), expected)

for stem in ['hann_1024_w16_f15', 'sine_1024_w16_f15']:
    x = (windows.hann(1024)*(32767/32768) if stem.startswith('hann')
         else .8*np.sin(2*np.pi*4*np.arange(1024)/1024))
    q = np.rint(x*32768).astype(np.int64)
    expected = (q & 65535).astype(np.uint64)
    np.testing.assert_array_equal(coe(results/(stem+'.coe')), expected)
    data = np.genfromtxt(results/(stem+'.csv'), delimiter=',', names=True, encoding='utf-8-sig')
    np.testing.assert_allclose(data['source_value'], x, atol=3e-14, rtol=3e-14)
    np.testing.assert_array_equal(data['integer_value'], q)

wave_groups = {}
with pathlib.Path(str(reference)+'.waves.csv').open() as f:
    for row in csv.DictReader(f):
        key = (row['wave'], int(row['n']), float(row['cycles']), float(row['phase']), float(row['duty']))
        wave_groups.setdefault(key, []).append(float(row['value']))
wave_error = 0.0
for (kind,n,cycles,phase,duty), actual in wave_groups.items():
    t = np.remainder(cycles*np.arange(n)/n+phase/360, 1.)
    if kind == 'Sine': y = np.sin(2*np.pi*t)
    elif kind == 'Cosine': y = np.cos(2*np.pi*t)
    elif kind == 'Triangle': y = signal.sawtooth(2*np.pi*t, width=duty)
    elif kind in ('Square','Pulse'): y = np.where(t<duty,1.,-1.)
    elif kind == 'SawUp': y = 2*t-1
    elif kind == 'SawDown': y = 1-2*t
    elif kind == 'DC': y = np.ones(n)
    expected = .8*y
    wave_error = max(wave_error,float(np.max(np.abs(expected-actual))))
    np.testing.assert_allclose(actual,expected,atol=2e-14,rtol=2e-14,err_msg=str((kind,n,cycles,phase,duty)))

report = {'status':'PASS', 'reference':f'SciPy {scipy.__version__}, NumPy {np.__version__}',
    'window_configurations':len(groups), 'window_samples':samples,
    'maximum_absolute_window_error':worst, 'worst_case':worst_case,
    'tolerance':'atol=3e-14, rtol=3e-14',
    'independent_coe_checks':'12 radix/width cases + Hann/sine examples; all words exactly equal',
    'wave_configurations':len(wave_groups), 'wave_samples':sum(len(v) for v in wave_groups.values()),
    'maximum_absolute_wave_error':wave_error,
    'scope':'Numerical/format verification; Vivado integration recorded separately.'}
(results/'independent-validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
