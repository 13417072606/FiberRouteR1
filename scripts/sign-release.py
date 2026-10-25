"""Administrator-only release signer. Private key remains on the release machine."""
import base64,json,pathlib,subprocess,sys,tempfile
if len(sys.argv)!=4:
    raise SystemExit('Usage: sign-release.py payload.json private-key.pem manifest.json')
payload=pathlib.Path(sys.argv[1]).read_bytes()
json.loads(payload)
with tempfile.TemporaryDirectory() as temp:
    signature=pathlib.Path(temp)/'signature'
    subprocess.run(['openssl','dgst','-sha256','-sign',sys.argv[2],'-sigopt','rsa_padding_mode:pss','-sigopt','rsa_pss_saltlen:32','-out',str(signature),sys.argv[1]],check=True)
    envelope={'payload':base64.b64encode(payload).decode(),'signature':base64.b64encode(signature.read_bytes()).decode()}
    pathlib.Path(sys.argv[3]).write_text(json.dumps(envelope,separators=(',',':')),encoding='utf-8')
