"""Frozen Windows entry; normal imports let PyInstaller collect stdlib modules."""
from app import main
if __name__=='__main__':main()
