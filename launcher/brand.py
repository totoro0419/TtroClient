"""Single product metadata source. Compatibility IDs are not a product name."""
import json, os
from pathlib import Path
PRODUCT = json.loads((Path(__file__).parent/'product.json').read_text(encoding='utf-8'))
COMPAT = PRODUCT['compatibility']
JAR_NAME = f"{PRODUCT['artifact']}-{PRODUCT['version']}.jar"
def default_data_path(base=None):
 root=Path(base) if base is not None else Path(os.getenv('LOCALAPPDATA',Path.home()/'.local/share'))
 current=root/PRODUCT['dataFolder'];legacy=root/COMPAT['legacyDataFolder']
 # Never move worlds, overwrite a newer installation, or silently create an empty
 # profile beside an existing installation. Explicit --data still takes priority.
 return legacy if not (current/'state.json').exists() and (legacy/'state.json').exists() else current
