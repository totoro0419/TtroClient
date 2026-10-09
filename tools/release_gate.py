"""Validate a maintainer's actual Windows E2E attestation against the exact CI assets.

This does not execute or impersonate account login. Missing evidence fails closed.
"""
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path
import uuid
import zipfile

REQUIRED = (
    'freshSetup', 'microsoftLogin', 'minecraftOwnership', 'play',
    'minecraft189', 'ttroLoaded', 'localWorld', 'restartReplay', 'repair',
    'resourcePackCardInstall', 'resourcePackInGame', 'modInstall', 'uninstallKeepsData',
)

def validate(report, assets, sha, run_id):
    assert report.get('schema') == 1 and report.get('repository') == 'totoro0419/TtroClient', 'Wrong evidence schema/repository'
    assert report.get('sourceCommit') == sha, 'E2E evidence is for a different commit'
    assert str(report.get('ciRunId')) == str(run_id), 'E2E evidence is for a different CI distribution'
    completed = dt.datetime.fromisoformat(report['completedAtUtc'].replace('Z', '+00:00'))
    assert completed.tzinfo and dt.timedelta(0) <= dt.datetime.now(dt.timezone.utc) - completed <= dt.timedelta(days=14), 'E2E evidence must be recent and dated in UTC'
    for check in REQUIRED:
        assert report.get('checks', {}).get(check) is True, 'Unverified owner E2E gate: ' + check
    for filename, field in [('TtroClient-Setup.exe', 'setupSha256'), ('TtroClient-Portable.zip', 'portableSha256')]:
        with (assets / filename).open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        assert digest == report.get(field), 'E2E asset hash mismatch: ' + filename
    with zipfile.ZipFile(assets / 'TtroClient-Portable.zip') as archive:
        settings = json.loads(archive.read('launcher-settings.json'))
        identifier = str(uuid.UUID(settings['microsoftClientId']))
        assert uuid.UUID(identifier).int and identifier == str(uuid.UUID(report['microsoftClientId'])), 'Invalid/mismatched public app ID'
        build = json.loads(archive.read('build-info.json'))
        assert build['sourceCommit'] == sha and str(build['ciRunId']) == str(run_id), 'Package source provenance mismatch'

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--sha', required=True)
    parser.add_argument('--run-id', required=True)
    args = parser.parse_args()
    validate(json.loads(args.report.read_text(encoding='utf-8-sig')), args.assets, args.sha, args.run_id)
    print('Exact distribution has complete owner E2E attestation; this validator does not perform login itself.')
