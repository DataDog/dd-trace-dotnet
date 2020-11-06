"""
customaction namespaced tasks
"""
from __future__ import print_function

import glob
import os
import shutil
import sys

from invoke import task
from invoke.exceptions import Exit, ParseError, Failure, UnexpectedExit
from .ssm import get_signing_cert, get_pfx_pass

projects_to_restore = [
    "Datadog.Trace", "Datadog.Trace.AspNet", "Datadog.Trace.ClrProfiler.Managed", "Datadog.Trace.ClrProfiler.Managed.Core", "Datadog.Trace.ClrProfiler.Managed.Loader"
]
@task
def build(ctx, vstudio_root=None, arch="All", major_version='7', debug=False):
    """
    Build the custom action library for the agent
    """

    if sys.platform != 'win32':
        print("DotNet Tracer build is only for Win32")
        raise Exit(code=1)

    print("arch is {}".format(arch))
    this_dir = os.getcwd()
    solution_dir = os.getcwd()
    tracer_home = os.path.join(solution_dir, "tracer_home")
    msi_home = os.path.join(solution_dir, "msi")

    pfxfile = None
    pfxpass = None
    remove_pfx = True
    cmd = ""
    configuration = "Release"
    if debug:
        configuration = "Debug"
    ctx.run("nuget restore {solution_dir}\\Datadog.Trace.Minimal.sln".format(solution_dir=solution_dir))
    for proj in projects_to_restore:
        try:
            # some projects may not exist in older commits, which is fine
            nuget_cmd = "nuget restore {solution_dir}\\src\\{proj}\\{proj}.csproj -PackagesDirectory packages".format(
                solution_dir = solution_dir,
                proj=proj
            )
            ctx.run(nuget_cmd)
        except:
            print("Failed to restore nuget package {proj}\n".format(proj=proj))
            pass

    cmd = "msbuild {solution_dir}\\Datadog.Trace.proj /t:{target} /p:Platform={arch} /p:Configuration={config} /p:TracerHomeDirectory={tracer_home} /p:RunWixToolsOutOfProc=true /p:MsiOutputPath={msihome}"

    run_cmd = cmd.format(
        solution_dir=solution_dir,
        target="CreateHomeDirectory",
        arch=arch,
        config=configuration,
        tracer_home=tracer_home,
        msihome=msi_home
    )
    ctx.run(run_cmd)
    ## pull the signing cert and password
    try:
        if sys.platform == 'win32' and os.environ.get('SIGN_WINDOWS'):
            # get certificate and password from ssm
            pfxfile = get_signing_cert(ctx)
            pfxpass = get_pfx_pass(ctx)
        else:
            remove_pfx = False

        if pfxfile and pfxpass:
            for f in glob.iglob("tracer_home/**/datadog*.dll", recursive=True):
                sign_binary(ctx, f, pfxfile, pfxpass)
        else:
            for f in glob.iglob("tracer_home/**/datadog*.dll", recursive=True):
                print("Not signing: {f}\n".format(f=f))

        run_cmd = cmd.format(
            solution_dir=solution_dir,
            target="MsiOnly",
            arch=arch,
            config=configuration,
            tracer_home=tracer_home,
            msihome=msi_home
        )
        ctx.run(run_cmd)
        if pfxfile and pfxpass:
            for f in glob.iglob("msi/**/*.msi", recursive=True):
                sign_binary(ctx, f, pfxfile, pfxpass)
        else:
            for f in glob.iglob("msi/**/*.msi", recursive=True):
                print("Not signing: {f}\n".format(f=f))

    except Exception as e:
        if pfxfile and remove_pfx:
            os.remove(pfxfile)
        raise

    if pfxfile and remove_pfx:
        os.remove(pfxfile)



@task
def clean(ctx, arch="x64", debug=False):
    configuration = "Release"
    if debug:
        configuration = "Debug"

    if arch is not None and arch == "x86":
        srcdll = "{}\\cal\\{}".format(CUSTOM_ACTION_ROOT_DIR, configuration)
    else:
        srcdll = "{}\\cal\\x64\\{}".format(CUSTOM_ACTION_ROOT_DIR, configuration)
    shutil.rmtree(srcdll, BIN_PATH)

timestamp_server = "http://timestamp.digicert.com/"
def sign_binary(ctx, path, certfile, certpass):

    print("Signing {}\n".format(path))
    cmd = "signtool sign /f {certfile} /p {certpass} /t {timestamp_server} {file}".format(
        certfile=certfile, 
        certpass=certpass, 
        timestamp_server=timestamp_server,
        file=path)

    ## is harder to debug, but suppress stdin/stdout/stderr echo (hide = true) so we 
    ## don't print the password
    try:
        ctx.run(cmd, hide=True)
    except UnexpectedExit as e:
        error = "(UE) Failed to sign file {file}\n".format(file=path)
        print(error)
        e.result.command = "redacted"
        raise UnexpectedExit(e.result, e.reason)

    except Failure as e:
        error = "(F) Failed to sign file {file}\n".format(file=path)
        print(error)
        e.result.command = "redacted"
        raise Failure(e.result, e.reason)

    except Exit as e:
        error = "(E) Failed to sign file {file}\n".format(file=path)
        print(error)
        raise
