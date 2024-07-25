import subprocess
import sys
import os
import shutil

def run_command(command):
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, shell=True)
    stdout, stderr = process.communicate()
    if process.returncode != 0:
        print(f"Error executing command: {command}")
        print(stderr.decode())
        sys.exit(1)
    return stdout.decode()

def copy_dlls():
    # Define the source and destination directories
    source_dir = os.path.join('..', 'Neighborly', 'bin', 'Release', 'net8.0')
    dest_dir = os.path.join('src', 'neighborly')

    # Ensure the destination directory exists
    os.makedirs(dest_dir, exist_ok=True)

    # Copy all DLL files from source to destination
    for filename in os.listdir(source_dir):
        if filename.endswith('.dll'):
            full_file_name = os.path.join(source_dir, filename)
            if os.path.isfile(full_file_name):
                shutil.copy(full_file_name, dest_dir)
                print(f"Copied {filename} to {dest_dir}")

def build_package():
    # Ensure we're in the correct directory
    script_dir = os.path.dirname(os.path.abspath(__file__))
    os.chdir(script_dir)

    # Install required packages
    print("Installing required packages...")
    run_command("pip install build twine")

    # Copy DLL files
    print("Copying DLL files...")
    copy_dlls()

    # Build the package
    print("Building the package...")
    run_command("python -m build")

    print("Package built successfully.")

if __name__ == "__main__":
    build_package()
