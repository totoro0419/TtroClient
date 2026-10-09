import copy
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

spec=importlib.util.spec_from_file_location('release_gate',Path(__file__).resolve().parents[1]/'tools/release_gate.py')
gate=importlib.util.module_from_spec(spec);spec.loader.exec_module(gate)

class ReleaseGateTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.assets=Path(self.temp.name);self.sha='a'*40;self.cid='81f015fe-9269-42d7-90ad-67743580bb80'
  # Synthetic test ID is confined to this fixture, never a product configuration.
  (self.assets/'TtroClient-Setup.exe').write_bytes(b'fixture')
  with zipfile.ZipFile(self.assets/'TtroClient-Portable.zip','w') as z:
   z.writestr('launcher-settings.json',json.dumps({'microsoftClientId':self.cid}))
   z.writestr('build-info.json',json.dumps({'sourceCommit':self.sha,'ciRunId':'123'}))
  self.report={'schema':1,'repository':'totoro0419/TtroClient','sourceCommit':self.sha,'ciRunId':'123','completedAtUtc':dt.datetime.now(dt.timezone.utc).isoformat(),'microsoftClientId':self.cid,'checks':dict.fromkeys(gate.REQUIRED,True)}
  for filename,field in [('TtroClient-Setup.exe','setupSha256'),('TtroClient-Portable.zip','portableSha256')]:self.report[field]=hashlib.sha256((self.assets/filename).read_bytes()).hexdigest()
 def tearDown(self):self.temp.cleanup()
 def test_complete_exact_attestation(self):gate.validate(self.report,self.assets,self.sha,'123')
 def test_missing_every_required_gate(self):
  for key in gate.REQUIRED:
   with self.subTest(key=key):
    report=copy.deepcopy(self.report);del report['checks'][key]
    with self.assertRaises(AssertionError):gate.validate(report,self.assets,self.sha,'123')
 def test_false_is_not_pass(self):
  self.report['checks']['microsoftLogin']='true'
  with self.assertRaises(AssertionError):gate.validate(self.report,self.assets,self.sha,'123')
 def test_other_commit(self):
  with self.assertRaises(AssertionError):gate.validate(self.report,self.assets,'b'*40,'123')
 def test_other_ci_distribution(self):
  with self.assertRaises(AssertionError):gate.validate(self.report,self.assets,self.sha,'456')
 def test_replaced_setup(self):
  (self.assets/'TtroClient-Setup.exe').write_bytes(b'other build')
  with self.assertRaises(AssertionError):gate.validate(self.report,self.assets,self.sha,'123')
 def test_other_public_id(self):
  self.report['microsoftClientId']='00000000-0000-0000-0000-000000000000'
  with self.assertRaises(AssertionError):gate.validate(self.report,self.assets,self.sha,'123')
