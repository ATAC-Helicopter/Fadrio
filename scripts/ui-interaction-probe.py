import ctypes as c
import os
import re
import signal
import subprocess
import sys
import time

window = int(sys.argv[1], 16)
root_path = sys.argv[2]
x = c.CDLL('libX11.so.6')
t = c.CDLL('libXtst.so.6')
x.XOpenDisplay.restype = c.c_void_p
x.XDefaultRootWindow.argtypes = [c.c_void_p]
x.XDefaultRootWindow.restype = c.c_ulong
x.XGetInputFocus.argtypes = [c.c_void_p, c.POINTER(c.c_ulong), c.POINTER(c.c_int)]
x.XSetInputFocus.argtypes = [c.c_void_p, c.c_ulong, c.c_int, c.c_ulong]
x.XRaiseWindow.argtypes = [c.c_void_p, c.c_ulong]
x.XFlush.argtypes = [c.c_void_p]
x.XCloseDisplay.argtypes = [c.c_void_p]
x.XTranslateCoordinates.argtypes = [c.c_void_p, c.c_ulong, c.c_ulong, c.c_int, c.c_int, c.POINTER(c.c_int), c.POINTER(c.c_int), c.POINTER(c.c_ulong)]
x.XStringToKeysym.argtypes = [c.c_char_p]
x.XStringToKeysym.restype = c.c_ulong
x.XKeysymToKeycode.argtypes = [c.c_void_p, c.c_ulong]
x.XKeysymToKeycode.restype = c.c_uint
x.XQueryPointer.argtypes = [c.c_void_p, c.c_ulong, c.POINTER(c.c_ulong), c.POINTER(c.c_ulong), c.POINTER(c.c_int), c.POINTER(c.c_int), c.POINTER(c.c_int), c.POINTER(c.c_int), c.POINTER(c.c_uint)]
t.XTestFakeMotionEvent.argtypes = [c.c_void_p, c.c_int, c.c_int, c.c_int, c.c_ulong]
t.XTestFakeButtonEvent.argtypes = [c.c_void_p, c.c_uint, c.c_int, c.c_ulong]
t.XTestFakeKeyEvent.argtypes = [c.c_void_p, c.c_uint, c.c_int, c.c_ulong]
d = x.XOpenDisplay(None)
assert d, 'No X11 display'
root = x.XDefaultRootWindow(d)
old_focus, revert = c.c_ulong(), c.c_int()
x.XGetInputFocus(d, c.byref(old_focus), c.byref(revert))
root_ret, child = c.c_ulong(), c.c_ulong()
old_x, old_y, win_x, win_y, mask = c.c_int(), c.c_int(), c.c_int(), c.c_int(), c.c_uint()
x.XQueryPointer(d, root, c.byref(root_ret), c.byref(child), c.byref(old_x), c.byref(old_y), c.byref(win_x), c.byref(win_y), c.byref(mask))
origin_x, origin_y = c.c_int(), c.c_int()
x.XTranslateCoordinates(d, window, root, 0, 0, c.byref(origin_x), c.byref(origin_y), c.byref(child))

def move(px, py):
    t.XTestFakeMotionEvent(d, -1, origin_x.value + px, origin_y.value + py, 0)
    x.XFlush(d)
    time.sleep(.08)

def button(down):
    t.XTestFakeButtonEvent(d, 1, down, 0)
    x.XFlush(d)
    time.sleep(.08)

def key(name):
    code = x.XKeysymToKeycode(d, x.XStringToKeysym(name.encode()))
    t.XTestFakeKeyEvent(d, code, 1, 0)
    t.XTestFakeKeyEvent(d, code, 0, 0)
    x.XFlush(d)
    time.sleep(.08)

def state():
    output = subprocess.check_output(['dotnet', root_path + '/src/Fadrio.Cli/bin/Debug/net10.0/fadrioctl.dll', 'apps'], text=True)
    assert 'Application: Fadrio Fixture' in output, output
    return int(re.search(r'Volume: (\d+)%', output)[1]), 'Muted: true' in output

try:
    x.XRaiseWindow(d, window)
    x.XSetInputFocus(d, window, 2, 0)
    x.XFlush(d)
    time.sleep(.5)
    x.XTranslateCoordinates(d, window, root, 0, 0, c.byref(origin_x), c.byref(origin_y), c.byref(child))
    if len(sys.argv) > 3:
        # The shell passes only its own isolated daemon PID.
        move(73, 281)
        button(1)
        move(100, 281)
        os.kill(int(sys.argv[3]), signal.SIGTERM)
        time.sleep(1)
        move(146, 281)
        button(0)
        print('Disconnected the isolated daemon during an active drag', flush=True)
        sys.exit(0)
    assert state()[0] == 100
    move(314, 281)
    button(1)
    for px in [290, 265, 240, 215, 190, 165, 146]: move(px, 281)
    button(0)
    volume, muted = state()
    assert 35 <= volume <= 45, (volume, muted)
    print('Rendered slider drag reached', volume, 'percent', flush=True)
    # Reassert focus after the separate CLI probe; the desktop may redirect it.
    x.XSetInputFocus(d, window, 2, 0)
    x.XFlush(d)
    time.sleep(.15)
    key('Home')
    for _ in range(10): key('Right')
    volume, muted = state()
    assert volume == 10, (volume, muted)
    print('Keyboard Home + ten Right keys reached 10 percent', flush=True)
    move(62, 327)
    button(1)
    button(0)
    assert state()[1]
    print('Rendered mute button muted the isolated stream', flush=True)
finally:
    t.XTestFakeButtonEvent(d, 1, 0, 0)
    t.XTestFakeMotionEvent(d, -1, old_x.value, old_y.value, 0)
    if old_focus.value > 1: x.XSetInputFocus(d, old_focus.value, revert.value, 0)
    x.XFlush(d)
    x.XCloseDisplay(d)
