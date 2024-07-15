import subprocess
import sys
import os

def run_command(command):
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, shell=True)
    stdout, stderr = process.communicate()
    if process.returncode != 0:
        print(f"Error executing command: {command}")
        print(stderr.decode())
        sys.exit(1)
    return stdout.decode()

def build_package():
    # Ensure we're in the correct directory
    script_dir = os.path.dirname(os.path.abspath(__file__))
    os.chdir(script_dir)

    # Install required packages
    print("Installing required packages...")
    run_command("pip install build twine")

    # Build the package
    print("Building the package...")
    run_command("python -m build")

    print("Package built successfully.")

if __name__ == "__main__":
    build_package()