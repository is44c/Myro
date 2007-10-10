# graphics.py
"""Simple object oriented graphics library

Original code by John Zelle
Updates       by Doug Blank

The library is designed to make it very easy for novice programmers to
experiment with computer graphics in an object oriented fashion. It is
written by John Zelle for use with the book "Python Programming: An
Introduction to Computer Science" (Franklin, Beedle & Associates).

LICENSE: This is open-source software released under the terms of the
GPL (http://www.gnu.org/licenses/gpl.html).

PLATFORMS: The package is a wrapper around Tkinter and should run on
any platform where Tkinter is available.

INSTALLATION: Put this file somewhere where Python can see it.

OVERVIEW: There are two kinds of objects in the library. The GraphWin
class implements a window where drawing can be done and various
GraphicsObjects are provided that can be drawn into a GraphWin. As a
simple example, here is a complete program to draw a circle of radius
10 centered in a 100x100 window:

--------------------------------------------------------------------
from graphics import *

def main():
    win = GraphWin("My Circle", 100, 100)
    c = Circle(Point(50,50), 10)
    c.draw(win)
    win.getMouse() // Pause to view result

main()
--------------------------------------------------------------------
GraphWin objects support coordinate transformation through the
setCoords method and pointer-based input through getMouse.

The library provides the following graphical objects:
    Point
    Line
    Circle
    Oval
    Rectangle
    Polygon
    Text
    Entry (for text-based input)
    Image

Various attributes of graphical objects can be set such as
outline-color, fill-color and line-width. Graphical objects also
support moving and hiding for animation effects.

The library also provides a very simple class for pixel-based image
manipulation, Pixmap. A pixmap can be loaded from a file and displayed
using an Image object. Both getPixel and setPixel methods are provided
for manipulating the image.

DOCUMENTATION: For complete documentation, see Chapter 5 of "Python
Programming: An Introduction to Computer Science" by John Zelle,
published by Franklin, Beedle & Associates.  Also see
http://mcsp.wartburg.edu/zelle/python for a quick reference"""

# Version 3.3 8/8/06
#     Added checkMouse method to GraphWin
# Version 3.2.2 5/30/05
#     Cleaned up handling of exceptions in Tk thread. The graphics package
#     now raises an exception if attempt is made to communicate with
#     a dead Tk thread.
# Version 3.2.1 5/22/05
#     Added shutdown function for tk thread to eliminate race-condition
#        error "chatter" when main thread terminates
#     Renamed various private globals with _
# Version 3.2 5/4/05
#     Added Pixmap object for simple image manipulation.
# Version 3.1 4/13/05
#     Improved the Tk thread communication so that most Tk calls
#        do not have to wait for synchonization with the Tk thread.
#        (see _tkCall and _tkExec)
# Version 3.0 12/30/04
#     Implemented Tk event loop in separate thread. Should now work
#        interactively with IDLE. Undocumented autoflush feature is
#        no longer necessary. Its default is now False (off). It may
#        be removed in a future version.
#     Better handling of errors regarding operations on windows that
#       have been closed.
#     Addition of an isClosed method to GraphWindow class.

# Version 2.2 8/26/04
#     Fixed cloning bug reported by Joseph Oldham.
#     Now implements deep copy of config info.
# Version 2.1 1/15/04
#     Added autoflush option to GraphWin. When True (default) updates on
#        the window are done after each action. This makes some graphics
#        intensive programs sluggish. Turning off autoflush causes updates
#        to happen during idle periods or when flush is called.
# Version 2.0
#     Updated Documentation
#     Made Polygon accept a list of Points in constructor
#     Made all drawing functions call TK update for easier animations
#          and to make the overall package work better with
#          Python 2.3 and IDLE 1.0 under Windows (still some issues).
#     Removed vestigial turtle graphics.
#     Added ability to configure font for Entry objects (analogous to Text)
#     Added setTextColor for Text as an alias of setFill
#     Changed to class-style exceptions
#     Fixed cloning of Text objects

# Version 1.6
#     Fixed Entry so StringVar uses _root as master, solves weird
#            interaction with shell in Idle
#     Fixed bug in setCoords. X and Y coordinates can increase in
#           "non-intuitive" direction.
#     Tweaked wm_protocol so window is not resizable and kill box closes.

# Version 1.5
#     Fixed bug in Entry. Can now define entry before creating a
#     GraphWin. All GraphWins are now toplevel windows and share
#     a fixed root (called _root).

# Version 1.4
#     Fixed Garbage collection of Tkinter images bug.
#     Added ability to set text atttributes.
#     Added Entry boxes.

import time, os, sys
import Tkinter
try: 	 
     import Image as PyImage	     import Image as PyImage
     import ImageTk	     import ImageTk
 except: 	 
     print >> sys.stderr, "WARNING: Image not found; do you need Python Imaging Library?" 	 
 tk = Tkinter	 tk = Tkinter
 try: 	 
     from numpy import array	     from numpy import array
 except: 	 
     print >> sys.stderr, "WARNING: numpy not found" 	 

import math
try:
    import tkSnack
    #_tkExec(tkSnack.initializeSnack, myro.globvars.gui)
except:
    tkSnack = None
    print >> sys.stderr, "WARNING: sound did not load; need tkSnack?"

##########################################################################
# Module Exceptions

import exceptions

class GraphicsError(exceptions.Exception):
    """Generic error class for graphics module exceptions."""
    def __init__(self, args=None):
        self.args=args

OBJ_ALREADY_DRAWN = "Object currently drawn"
UNSUPPORTED_METHOD = "Object doesn't support operation"
BAD_OPTION = "Illegal option value"
DEAD_THREAD = "Graphics thread quit unexpectedly"

###########################################################################
# Support to run Tk in a separate thread

import myro.globvars
from copy import copy
from Queue import Queue
import thread
import atexit

_tk_request = Queue(0)
_tk_result = Queue(1)
_POLL_INTERVAL = 10

_root = None
_thread_running = True
_exception_info = None

def _tk_thread():
    global _root
    try:
        _root = tk.Tk()
        myro.globvars.gui = _root
        _root.withdraw()
        _root.after(_POLL_INTERVAL, _tk_pump)
        _root.mainloop()
    except:
        _root = None
        print >> sys.stderr, "ERROR: graphics did not start"

def _tk_pump():
    global _thread_running
    while not _tk_request.empty():
        command,returns_value = _tk_request.get()
        try:
            result = command()
            if returns_value:
                _tk_result.put(result)
        except:
            _thread_running = False
            if returns_value:
                _tk_result.put(None) # release client
            raise # re-raise the exception -- kills the thread
    if _thread_running:
        try:
            _root.after(_POLL_INTERVAL, _tk_pump)
        except:
            print "Graphics: Can't pump anymore"

if myro.globvars.runtkthread:
    def _tkCall(f, *args, **kw):
        # execute synchronous call to f in the Tk thread
        # this function should be used when a return value from
        #   f is required or when synchronizing the threads.
        # call to _tkCall in Tk thread == DEADLOCK !
        if not _thread_running:
            raise GraphicsError, DEAD_THREAD
        def func():
            return f(*args, **kw)
        _tk_request.put((func,True),True)
        result = _tk_result.get(True)
        return result

    def _tkExec(f, *args, **kw):
        # schedule f to execute in the Tk thread. This function does
        #   not wait for f to actually be executed.
        #global _exception_info
        #_exception_info = None
        if not _thread_running:
            raise GraphicsError, DEAD_THREAD
        def func():
            return f(*args, **kw)
        _tk_request.put((func,False),True)
        #if _exception_info is not None:
        #    raise GraphicsError, "Invalid Operation: %s" % str(_exception_info)

else:
    def _tkCall(f, *args, **kw):
        return f(*args, **kw)

    def _tkExec(f, *args, **kw):
        return f(*args, **kw)

def _tkShutdown():
    # shutdown the tk thread
    global _thread_running
    #_tkExec(sys.exit)
    _thread_running = False
    time.sleep(.5) # give tk thread time to quit

# Fire up the separate Tk thread
if myro.globvars.runtkthread:
    thread.start_new_thread(_tk_thread,())
else:
    _root = tk.Tk()
    myro.globvars.gui = _root
    _root.withdraw()


def moveToTop(window):
    if not "darwin" in sys.platform and "win" in sys.platform:        
        print "this is windows", sys.platform
        window.wm_attributes("-topmost", 1)
    window.lift()
    window.focus() 

############################################################################
# Graphics classes start here

import tkFileDialog, tkColorChooser, Dialog
from myro.widgets import AlertDialog

def distance(color1, color2):
    rgb1 = color1.getRGB()
    rgb2 = color2.getRGB()
    return math.sqrt((rgb1[0] - rgb2[0]) ** 2 + (rgb1[1] - rgb2[1]) ** 2 + (rgb1[2] - rgb2[2]) ** 2)

class AskDialog(AlertDialog):
    def __init__(self, title, qlist):
        _tkCall(self.__init_help, title, qlist)

    def run(self):
        return _tkCall(self.Show)

    def stop(self):
        return _tkCall(self.DialogCleanup)

    def __init_help(self, title, qlist):
        AlertDialog.__init__(self, _root, title)
        self.title = title
        self.qlist = qlist
        self.textbox = {}
        moveToTop(self.top)
        self.top.bind("<Return>", lambda event: self.OkPressed())
      
    def SetupDialog(self):
        AlertDialog.SetupDialog(self)
        self.bitmap['bitmap'] = 'question'
        first = 1
        for text in self.qlist.keys():
            default = self.qlist[text]
            if "password" in text.lower():
                self.CreateTextBox(text, width=30, default=default, show="*")
            else:
                self.CreateTextBox(text, width=30, default=default)
            if first:
                self.textbox[text].focus_set()
                first = 0
        self.CreateButton("Ok", self.OkPressed)
        self.CreateButton("Cancel", self.CancelPressed)

def askQuestion(question, answers = ["Yes", "No"], title = "Myro Question",
                default = 0, bitmap=Dialog.DIALOG_ICON):
    """ Displays a question and returns answer. """
    d = _tkCall(Dialog.Dialog, myro.globvars.gui,
                title=title, default=default, bitmap=bitmap,
                text=question, strings=answers)
    return answers[int(d.num)]


def pickAFile():
    """ Returns a filename """
    path = _tkCall(tkFileDialog.askopenfilename)
    return path

def pickAColor():
    """ Returns an RGB color tuple """
    color = _tkCall(tkColorChooser.askcolor)
    if color[0] != None:
        return Color(color[0][0], color[0][1], color[0][2])

def pickAFolder():
    """ Returns a folder path/name """
    folder = _tkCall(tkFileDialog.askdirectory)
    if folder == '':
        folder = myro.globvars.mediaFolder
    return folder

class GraphWin(tk.Canvas):

    """A GraphWin is a toplevel window for displaying graphics."""

    def __init__(self, title="Graphics Window",
                 width=200, height=200, autoflush=False):
        _tkCall(self.__init_help, title, width, height, autoflush)
 
    def __init_help(self, title, width, height, autoflush):
        master = tk.Toplevel(_root)
        master.protocol("WM_DELETE_WINDOW", self.__close_help)
        tk.Canvas.__init__(self, master, width=width, height=height)
        self.status = tk.Label(self.master, bd=1, relief=tk.SUNKEN,
                               anchor=tk.W)
        self.status.pack(fill=tk.X, side="bottom")

        moveToTop(self.master)
        self.master.title(title)
        self.pack()
        master.resizable(0,0)
        self.foreground = "black"
        self.items = []
        self.mouseX = None
        self.mouseY = None
        self.bind("<Button-1>", self._onClick)
        self.bind("<ButtonRelease-1>", self._onRelease)
        self.height = height
        self.width = width
        self.autoflush = autoflush
        self._mouseCallback = None
        self._mouseCallbackRelease = None
        self.trans = None
        self.closed = False
        # at least flash:
        self.master.after(50, self.master.deiconify)
        self.master.after(70, self.master.tkraise)
        _root.update()

    def setStatusDirect(self, format=""):
        self.status.config(text=format)

    def setStatus(self, format=""):
        self._setStatus(format)

    def _setStatus(self, format=""):
        _tkCall(self.status.config, text=format)

## Trying to get IDLE subprocess windows to be on top!
##    def toFront(self):
##        _tkCall(self.__toFront_help)
##
##    def __toFront_help(self):
##        _root.tkraise()
##        #self.master.after(50, self.master.deiconify)
##        #self.master.after(70, self.master.tkraise)
##        #self.master.after(70, self.master.focus)

    def __checkOpen(self):
        if self.closed:
            raise GraphicsError, "window is closed"

    def setBackground(self, color):
        """Set background color of the window"""
        self.__checkOpen()
        _tkExec(self.config, bg=color)
        #self.config(bg=color)
        
    def setCoords(self, x1, y1, x2, y2):
        """Set coordinates of window to run from (x1,y1) in the
        lower-left corner to (x2,y2) in the upper-right corner."""
        self.trans = Transform(self.width, self.height, x1, y1, x2, y2)

    def close(self):
        if self.closed: return
        _tkCall(self.__close_help)
        
    def __close_help(self):
        """Close the window"""
        self.closed = True
        self.master.destroy()
        _root.update()

    def isClosed(self):
        return self.closed

    def __autoflush(self):
        if self.autoflush:
            _tkCall(_root.update)
    
    def plot(self, x, y, color="black"):
        """Set pixel (x,y) to the given color"""
        self.__checkOpen()
        xs,ys = self.toScreen(x,y)
        #self.create_line(xs,ys,xs+1,ys, fill=color)
        _tkExec(self.create_line,xs,ys,xs+1,ys,fill=color,tag="line")
        self.__autoflush()
        
    def plotPixel(self, x, y, color="black"):
        """Set pixel raw (independent of window coordinates) pixel
        (x,y) to color"""
        self.__checkOpen()
        #self.create_line(x,y,x+1,y, fill=color)
        _tkExec(self.create_line, x,y,x+1,y, fill=color,tag="line")
        self.__autoflush()
        
    def flush(self):
        """Update drawing to the window"""
        #self.update_idletasks()
        self.__checkOpen()
        _tkCall(self.update_idletasks)
        
    def getMouse(self):
        """Wait for mouse click and return Point object representing
        the click"""
        self.mouseX = None
        self.mouseY = None
        while self.mouseX == None or self.mouseY == None:
            #self.update()
            _tkCall(self.update)
            if self.isClosed(): raise GraphicsError, "getMouse in closed window"
            time.sleep(.1) # give up thread
        x,y = self.toWorld(self.mouseX, self.mouseY)
        self.mouseX = None
        self.mouseY = None
        return Point(x,y)

    def checkMouse(self):
        """Return mouse click last mouse click or None if mouse has
        not been clicked since last call"""
        if self.isClosed():
            raise GraphicsError, "checkMouse in closed window"
        _tkCall(self.update)
        if self.mouseX != None and self.mouseY != None:
            x,y = self.toWorld(self.mouseX, self.mouseY)
            self.mouseX = None
            self.mouseY = None
            return Point(x,y)
        else:
            return None
            
    def getHeight(self):
        """Return the height of the window"""
        return self.height
        
    def getWidth(self):
        """Return the width of the window"""
        return self.width
    
    def toScreen(self, x, y):
        trans = self.trans
        if trans:
            return self.trans.screen(x,y)
        else:
            return x,y
                      
    def toWorld(self, x, y):
        trans = self.trans
        if trans:
            return self.trans.world(x,y)
        else:
            return x,y
        
    def setMouseHandler(self, func):
        self._mouseCallback = func

    def setMouseReleaseHandler(self, func):
        self._mouseCallbackRelease = func
        
    def _onClick(self, e):
        self.mouseX = e.x
        self.mouseY = e.y
        if self._mouseCallback:
            self._mouseCallback(Point(e.x, e.y))

    def _onRelease(self, e):
        self.mouseX = e.x
        self.mouseY = e.y
        if self._mouseCallbackRelease:
            self._mouseCallbackRelease(Point(e.x, e.y))

class Transform:

    """Internal class for 2-D coordinate transformations"""
    
    def __init__(self, w, h, xlow, ylow, xhigh, yhigh):
        # w, h are width and height of window
        # (xlow,ylow) coordinates of lower-left [raw (0,h-1)]
        # (xhigh,yhigh) coordinates of upper-right [raw (w-1,0)]
        xspan = (xhigh-xlow)
        yspan = (yhigh-ylow)
        self.xbase = xlow
        self.ybase = yhigh
        self.xscale = xspan/float(w-1)
        self.yscale = yspan/float(h-1)
        
    def screen(self,x,y):
        # Returns x,y in screen (actually window) coordinates
        xs = (x-self.xbase) / self.xscale
        ys = (self.ybase-y) / self.yscale
        return int(xs+0.5),int(ys+0.5)
        
    def world(self,xs,ys):
        # Returns xs,ys in world coordinates
        x = xs*self.xscale + self.xbase
        y = self.ybase - ys*self.yscale
        return x,y


# Default values for various item configuration options. Only a subset of
#   keys may be present in the configuration dictionary for a given item
DEFAULT_CONFIG = {"fill":"",
          "outline":"black",
          "width":"1",
          "arrow":"none",
          "text":"",
          "justify":"center",
                  "font": ("helvetica", 12, "normal")}

class GraphicsObject:

    """Generic base class for all of the drawable objects"""
    # A subclass of GraphicsObject should override _draw and
    #   and _move methods.
    
    def __init__(self, options):
        # options is a list of strings indicating which options are
        # legal for this object.
        
        # When an object is drawn, canvas is set to the GraphWin(canvas)
        #    object where it is drawn and id is the TK identifier of the
        #    drawn shape.
        self.canvas = None
        self.id = None

        # config is the dictionary of configuration options for the widget.
        config = {}
        for option in options:
            config[option] = DEFAULT_CONFIG[option]
        self.config = config
        
    def setFill(self, color):
        """Set interior color to color"""
        self._reconfig("fill", color)
        
    def setOutline(self, color):
        """Set outline color to color"""
        self._reconfig("outline", color)
        
    def setWidth(self, width):
        """Set line weight to width"""
        self._reconfig("width", width)

    def draw(self, graphwin):

        """Draw the object in graphwin, which should be a GraphWin
        object.  A GraphicsObject may only be drawn into one
        window. Raises an error if attempt made to draw an object that
        is already visible."""

        if self.canvas and not self.canvas.isClosed(): raise GraphicsError, OBJ_ALREADY_DRAWN
        if graphwin.isClosed(): raise GraphicsError, "Can't draw to closed window"
        self.canvas = graphwin
        #self.id = self._draw(graphwin, self.config)
        self.id = _tkCall(self._draw, graphwin, self.config)
        if graphwin.autoflush:
            #_root.update()
            _tkCall(_root.update)

    def undraw(self):

        """Undraw the object (i.e. hide it). Returns silently if the
        object is not currently drawn."""
        
        if not self.canvas: return
        if not self.canvas.isClosed():
            #self.canvas.delete(self.id)
            _tkExec(self.canvas.delete, self.id)
            if self.canvas.autoflush:
                #_root.update()
                _tkCall(_root.update)
        self.canvas = None
        self.id = None

    def move(self, dx, dy):

        """move object dx units in x direction and dy units in y
        direction"""
        
        self._move(dx,dy)
        canvas = self.canvas
        if canvas and not canvas.isClosed():
            trans = canvas.trans
            if trans:
                x = dx/ trans.xscale 
                y = -dy / trans.yscale
            else:
                x = dx
                y = dy
            #self.canvas.move(self.id, x, y)
            _tkExec(self.canvas.move, self.id, x, y)
            if canvas.autoflush:
                #_root.update()
                _tkCall(_root.update)
           
    def _reconfig(self, option, setting):
        # Internal method for changing configuration of the object
        # Raises an error if the option does not exist in the config
        #    dictionary for this object
        if not self.config.has_key(option):
            raise GraphicsError, UNSUPPORTED_METHOD
        options = self.config
        options[option] = setting
        if self.canvas and not self.canvas.isClosed():
            #self.canvas.itemconfig(self.id, options)
            _tkExec(self.canvas.itemconfig, self.id, options)
            if self.canvas.autoflush:
                #_root.update()
                _tkCall(_root.update)

    def _draw(self, canvas, options):
        """draws appropriate figure on canvas with options provided
        Returns Tk id of item drawn"""
        pass # must override in subclass

    def _move(self, dx, dy):
        """updates internal state of object to move it dx,dy units"""
        pass # must override in subclass
         
class Point(GraphicsObject):
    def __init__(self, x, y):
        GraphicsObject.__init__(self, ["outline", "fill"])
        self.setFill = self.setOutline
        self.x = x
        self.y = y
        
    def _draw(self, canvas, options):
        x,y = canvas.toScreen(self.x,self.y)
        return canvas.create_rectangle(x,y,x+1,y+1,options,tag="rect")
        
    def _move(self, dx, dy):
        self.x = self.x + dx
        self.y = self.y + dy
        
    def clone(self):
        other = Point(self.x,self.y)
        other.config = self.config.copy()
        return other
                
    def getX(self): return self.x
    def getY(self): return self.y

class _BBox(GraphicsObject):
    # Internal base class for objects represented by bounding box
    # (opposite corners) Line segment is a degenerate case.
    
    def __init__(self, p1, p2, options=["outline","width","fill"]):
        GraphicsObject.__init__(self, options)
        self.p1 = p1.clone()
        self.p2 = p2.clone()

    def _move(self, dx, dy):
        self.p1.x = self.p1.x + dx
        self.p1.y = self.p1.y + dy
        self.p2.x = self.p2.x + dx
        self.p2.y = self.p2.y  + dy
                
    def getP1(self): return self.p1.clone()

    def getP2(self): return self.p2.clone()
    
    def getCenter(self):
        p1 = self.p1
        p2 = self.p2
        return Point((p1.x+p2.x)/2.0, (p1.y+p2.y)/2.0)
    
class Rectangle(_BBox):
    
    def __init__(self, p1, p2):
        _BBox.__init__(self, p1, p2)
    
    def _draw(self, canvas, options):
        p1 = self.p1
        p2 = self.p2
        x1,y1 = canvas.toScreen(p1.x,p1.y)
        x2,y2 = canvas.toScreen(p2.x,p2.y)
        return canvas.create_rectangle(x1,y1,x2,y2,options,tag="rect")
        
    def clone(self):
        other = Rectangle(self.p1, self.p2)
        other.config = self.config.copy()
        return other
        
class Oval(_BBox):
    
    def __init__(self, p1, p2):
        _BBox.__init__(self, p1, p2)
        
    def clone(self):
        other = Oval(self.p1, self.p2)
        other.config = self.config.copy()
        return other
   
    def _draw(self, canvas, options):
        p1 = self.p1
        p2 = self.p2
        x1,y1 = canvas.toScreen(p1.x,p1.y)
        x2,y2 = canvas.toScreen(p2.x,p2.y)
        return canvas.create_oval(x1,y1,x2,y2,options,tag="oval")
    
class Circle(Oval):
    
    def __init__(self, center, radius):
        p1 = Point(center.x-radius, center.y-radius)
        p2 = Point(center.x+radius, center.y+radius)
        Oval.__init__(self, p1, p2)
        self.radius = radius
        
    def clone(self):
        other = Circle(self.getCenter(), self.radius)
        other.config = self.config.copy()
        return other
        
    def getRadius(self):
        return self.radius
              
class Line(_BBox):
    
    def __init__(self, p1, p2):
        _BBox.__init__(self, p1, p2, ["arrow","fill","width"])
        self.setFill(DEFAULT_CONFIG['outline'])
        self.setOutline = self.setFill
   
    def clone(self):
        other = Line(self.p1, self.p2)
        other.config = self.config.copy()
        return other
    
    def _draw(self, canvas, options):
        p1 = self.p1
        p2 = self.p2
        x1,y1 = canvas.toScreen(p1.x,p1.y)
        x2,y2 = canvas.toScreen(p2.x,p2.y)
        return canvas.create_line(x1,y1,x2,y2,options,tag="line")
        
    def setArrow(self, option):
        if not option in ["first","last","both","none"]:
            raise GraphicsError, BAD_OPTION
        self._reconfig("arrow", option)
        

class Polygon(GraphicsObject):
    
    def __init__(self, *points):
        # if points passed as a list, extract it
        if len(points) == 1 and type(points[0] == type([])):
            points = points[0]
        self.points = map(Point.clone, points)
        GraphicsObject.__init__(self, ["outline", "width", "fill"])
        
    def clone(self):
        other = apply(Polygon, self.points)
        other.config = self.config.copy()
        return other

    def getPoints(self):
        return map(Point.clone, self.points)

    def _move(self, dx, dy):
        for p in self.points:
            p.move(dx,dy)
   
    def _draw(self, canvas, options):
        args = [canvas]
        for p in self.points:
            x,y = canvas.toScreen(p.x,p.y)
            args.append(x)
            args.append(y)
        args.append(options)
        return apply(GraphWin.create_polygon, args,tag="poly") 

class Text(GraphicsObject):
    
        def __init__(self, p, text):
            GraphicsObject.__init__(self, ["justify","fill","text","font"])
            self.setText(text)
            self.anchor = p.clone()
            self.setFill(DEFAULT_CONFIG['outline'])
            self.setOutline = self.setFill
            
        def _draw(self, canvas, options):
            p = self.anchor
            x,y = canvas.toScreen(p.x,p.y)
            return canvas.create_text(x,y,options,tag="text")
            
        def _move(self, dx, dy):
            self.anchor.move(dx,dy)
            
        def clone(self):
            other = Text(self.anchor, self.config['text'])
            other.config = self.config.copy()
            return other

        def setText(self,text):
            self._reconfig("text", text)
            
        def getText(self):
            return self.config["text"]
                
        def getAnchor(self):
            return self.anchor.clone()

        def setFace(self, face):
            if face in ['helvetica','arial','courier','times roman']:
                f,s,b = self.config['font']
                self._reconfig("font",(face,s,b))
            else:
                raise GraphicsError, BAD_OPTION

        def setSize(self, size):
            if 5 <= size <= 36:
                f,s,b = self.config['font']
                self._reconfig("font", (f,size,b))
            else:
                raise GraphicsError, BAD_OPTION

        def setStyle(self, style):
            if style in ['bold','normal','italic', 'bold italic']:
                f,s,b = self.config['font']
                self._reconfig("font", (f,s,style))
            else:
                raise GraphicsError, BAD_OPTION

        def setTextColor(self, color):
            self.setFill(color)


class Entry(GraphicsObject):

    def __init__(self, p, width):
        GraphicsObject.__init__(self, [])
        self.anchor = p.clone()
        #print self.anchor
        self.width = width
        #self.text = tk.StringVar(_root)
        #self.text.set("")
        self.text = _tkCall(tk.StringVar, _root)
        _tkCall(self.text.set, "")
        self.fill = "gray"
        self.color = "black"
        self.font = DEFAULT_CONFIG['font']
        self.entry = None

    def _draw(self, canvas, options):
        p = self.anchor
        x,y = canvas.toScreen(p.x,p.y)
        frm = tk.Frame(canvas.master)
        self.entry = tk.Entry(frm,
                              width=self.width,
                              textvariable=self.text,
                              bg = self.fill,
                              fg = self.color,
                              font=self.font)
        self.entry.pack()
        #self.setFill(self.fill)
        return canvas.create_window(x,y,window=frm)

    def getText(self):
        return _tkCall(self.text.get)

    def _move(self, dx, dy):
        self.anchor.move(dx,dy)

    def getAnchor(self):
        return self.anchor.clone()

    def clone(self):
        other = Entry(self.anchor, self.width)
        return _tkCall(self.__clone_help, other)

    def __clone_help(self, other):
        other.config = self.config.copy()
        other.text = tk.StringVar()
        other.text.set(self.text.get())
        other.fill = self.fill
        return other

    def setText(self, t):
        #self.text.set(t)
        _tkCall(self.text.set, t)
            
    def setFill(self, color):
        self.fill = color
        if self.entry:
            #self.entry.config(bg=color)
            _tkExec(self.entry.config,bg=color)

    def _setFontComponent(self, which, value):
        font = list(self.font)
        font[which] = value
        self.font = tuple(font)
        if self.entry:
            #self.entry.config(font=self.font)
            _tkExec(self.entry.config, font=self.font)

    def setFace(self, face):
        if face in ['helvetica','arial','courier','times roman']:
            self._setFontComponent(0, face)
        else:
            raise GraphicsError, BAD_OPTION

    def setSize(self, size):
        if 5 <= size <= 36:
            self._setFontComponent(1,size)
        else:
            raise GraphicsError, BAD_OPTION

    def setStyle(self, style):
        if style in ['bold','normal','italic', 'bold italic']:
            self._setFontComponent(2,style)
        else:
            raise GraphicsError, BAD_OPTION

    def setTextColor(self, color):
        self.color=color
        if self.entry:
            #self.entry.config(fg=color)
            _tkExec(self.entry.config,fg=color)

def makePixmap(picture):
    photoimage = ImageTk.PhotoImage(picture.image)
    return Pixmap(photoimage)

class Picture(object):
    def __init__(self):
        self.width = 0
        self.height = 0
        self.image = None
        self.filename = None
    def set(self, width, height, data=None, mode = "color"):
        self.width = width
        self.height = height
        if mode.lower() == "color":
            if data == None:
                data = array([0] * (height * width * 3), 'B')
            self.image = PyImage.frombuffer("RGB", (self.width, self.height),
                                            data, "raw", "RGB", 0, 1)
        elif mode.lower() == "image": 	 
             self.image = data.copy()
        else: # "gray", "blob"
            self.image = PyImage.frombuffer("L", (self.width, self.height),
                                            data, 'raw', "L", 0, 1)
        self.pixels = self.image.load()
        self.palette = self.image.getpalette()
        self.filename = 'Camera Image'
        if self.pixels == None:
            raise AttributeError, "Myro needs at least Python Imaging Library version 1.1.6"
        #self.image = ImageTk.PhotoImage(self.temp, master=_root)
    def load(self, filename):
        #self.image = tk.PhotoImage(file=filename, master=_root)
        self.image = PyImage.open(filename)
        self.pixels = self.image.load()
        self.width = self.image.size[0]
        self.height = self.image.size[1]
        self.palette = self.image.getpalette()
        self.filename = filename
        if self.pixels == None:
            raise AttributeError, "Myro needs at least Python Imaging Library version 1.1.6"
    def __repr__(self):
        return "<Picture instance (%d x %d)>" % (self.width, self.height)
    def getPixel(self, x, y):
        return Pixel( x, y, self)
    def getColor(self, x, y):
        retval = self.pixels[x, y]
        if self.image.mode == "P": # Palette
            # gif, need to look up color in palette
            return Color( self.palette[retval * 3 + 0],
                          self.palette[retval * 3 + 1],
                          self.palette[retval * 3 + 2])
        elif self.image.mode == "RGB": # 3 bytes
            return Color(retval)
        elif self.image.mode == "L": # Grayscale
            # gif, need to look up color in palette
            return Color(retval, retval, retval)
    def setColor(self, x, y, newColor):
        if self.image.mode == "P":
            # first look up closest color, get index
            minDistance = 10000000
            minIndex = 0
            for i in range(0, len(self.palette), 3):
                d = distance(newColor, Color(self.palette[i + 0],
                                             self.palette[i + 1],
                                             self.palette[i + 2]))
                if d < minDistance:
                    minDistance, minIndex= d, i
            # put that index in the position
            self.pixels[x, y] = minIndex
        elif self.image.mode == "RGB": # 3 tuple
            self.pixels[x, y] = tuple(newColor.getRGB())
        elif self.image.mode == "L": # 1 int
            self.pixels[x, y] = sum(newColor.getRGB())/3 # avg or the three values
    def getRGB(self, x, y):
        retval = self.pixels[x, y]
        if self.image.mode == "P":
            # gif, need to look up color in palette
            return ( int(self.palette[retval * 3 + 0]),
                     int(self.palette[retval * 3 + 1]),
                     int(self.palette[retval * 3 + 2]))
        elif self.image.mode == "RGB":
            return retval
        elif self.image.mode == "L":
            return (retval, retval, retval)

class Pixel(object):
    def __init__(self, x, y, picture):
        self.x = x
        self.y = y
        self.picture = picture
        self.pixels = picture.pixels
        # we might need this, for gifs:
        self.palette = self.picture.image.getpalette()
    def __repr__(self):
        return ("<Pixel instance (r=%d, g=%d, b=%d) " % tuple(self.getRGB())) + ("at (%d, %d)>" % (self.x, self.y))
    def getPixel(self, x, y):
        return Pixel( x, y, self.picture)
    def getColor(self):
        retval = self.pixels[self.x, self.y]
        if self.picture.image.mode == "P":
            # gif, need to look up color in palette
            return Color( self.palette[retval * 3 + 0],
                          self.palette[retval * 3 + 1],
                          self.palette[retval * 3 + 2])
        elif self.picture.image.mode == "RGB":
            return Color(retval)
        elif self.picture.image.mode == "L":
            return Color(retval, retval, retval)
    def setColor(self, newColor):
        if self.picture.image.mode == "P":
            # first look up closest color, get index
            minDistance = 10000000
            minIndex = 0
            for i in range(0, len(self.palette), 3):
                d = distance(newColor, Color(self.palette[i + 0],
                                             self.palette[i + 1],
                                             self.palette[i + 2]))
                if d < minDistance:
                    minDistance, minIndex= d, i
            # put that index in the position
            self.pixels[self.x, self.y] = minIndex
        elif self.picture.image.mode == "RGB":
            self.pixels[self.x, self.y] = tuple(newColor.getRGB())
        elif self.picture.image.mode == "L":
            self.pixels[self.x, self.y] = sum(newColor.getRGB())/3 # avg
    def getRGB(self):
        retval = self.pixels[self.x, self.y]
        if self.picture.image.mode == "P":
            # gif, need to look up color in palette
            return ( int(self.palette[retval * 3 + 0]),
                     int(self.palette[retval * 3 + 1]),
                     int(self.palette[retval * 3 + 2]))
        elif self.picture.image.mode == "RGB":
            return retval
        elif self.picture.image.mode == "L":
            return retval, retval, retval
    def __eq__(self, other):
        o1 = self.getRGB()
        o2 = other.getRGB()
        return (o1[0] == o2[0] and o1[1] == o2[1] and o1[2] == o2[2])
    def __sub__(self, other):
        o1 = self.getRGB()
        o2 = other.getRGB()
        return Color(o1[0] - o2[0], o1[1] - o2[1], o1[2] - o2[2])
    def __add__(self, other):
        o1 = self.getRGB()
        o2 = other.getRGB()
        return Color(o1[0] + o2[0], o1[1] + o2[1], o1[2] + o2[2])
    def makeLighter(self):
        r, g, b = self.getRGB()
        rgb = (int(max(min((255 - r) * .35 + r, 255), 0)),
               int(max(min((255 - g) * .35 + g, 255), 0)),
               int(max(min((255 - b) * .35 + b, 255), 0)))
        self.setColor(Color(rgb))
    def makeDarker(self):
        r, g, b = self.getRGB()
        rgb = (int(max(min(r * .65, 255), 0)),
               int(max(min(g * .65, 255), 0)),
               int(max(min(b * .65, 255), 0)))
        self.setColor(Color(rgb))

class Color(object):
    def __init__(self, *rgb):
        if len(rgb) == 1:
            self.rgb = rgb[0]
        elif len(rgb) == 3:
            self.rgb = rgb
        else:
            raise AttributeError, "invalid colors to Color; need 3 integers: red, green, blue"
        self.rgb = map(lambda v: int(max(min(v,255),0)), self.rgb)
    def __repr__(self):
        return "<Color instance (r=%d, g=%d, b=%d)>)" % tuple(self.rgb)
    def getColor(self):
        return Color(self.rgb)
    def setColor(self, color):
        self.rgb = color.getRGB()
    def getRGB(self):
        return self.rgb
    def __eq__(self, other):
        o1 = self.getRGB()
        o2 = other.getRGB()
        return (o1[0] == o2[0] and o1[1] == o2[1] and o1[2] == o2[2])
    def __sub__(self, other):
        o1 = self.getRGB()
        o2 = other.getRGB()
        return Color(o1[0] - o2[0], o1[1] - o2[1], o1[2] - o2[2])
    def __add__(self, other):
        o1 = self.getRGB()
        o2 = other.getRGB()
        return Color(o1[0] + o2[0], o1[1] + o2[1], o1[2] + o2[2])
    def makeLighter(self):
        r, g, b = self.rgb
        self.rgb = (int(max(min((255 - r) * .35 + r, 255), 0)),
                    int(max(min((255 - g) * .35 + g, 255), 0)),
                    int(max(min((255 - b) * .35 + b, 255), 0)))
    def makeDarker(self):
        r, g, b = self.rgb
        self.rgb = (int(max(min(r * .65, 255), 0)),
                    int(max(min(g * .65, 255), 0)),
                    int(max(min(b * .65, 255), 0)))

class Image(GraphicsObject):
    idCount = 0
    imageCache = {} # tk photoimages go here to avoid GC while drawn
    def __init__(self, *center_point_and_pixmap):
        """
        Create a Image where p = Point, pixmap is a filename or image.
        """
        GraphicsObject.__init__(self, [])     # initialize
        if len(center_point_and_pixmap) == 1: # assume image
            self.anchor = None
            pixmap = center_point_and_pixmap[0]
        elif len(center_point_and_pixmap) == 2: # assume point, image
            self.anchor = center_point_and_pixmap[0].clone()
            pixmap = center_point_and_pixmap[1]
        else:
            raise AttributeError, "invalid parameters to Image(); need 1 or 2"
        self.imageId = Image.idCount        # give this image a number
        Image.idCount = Image.idCount + 1 # increment global counter
        if type(pixmap) == type(""): # assume a filename
            self.img = _tkCall(tk.PhotoImage, file=pixmap, master=_root)
        else:
            self.img = pixmap.image
            # _tkCall(tk.PhotoImage, pixmap.image, master=_root)
            # 

    def refresh(self, canvas):
        _tkCall(self._refresh, canvas)

    def _refresh(self, canvas):
        p = self.anchor
        x,y = canvas.toScreen(p.x,p.y)
        self.imageCache[self.imageId] = self.img
        canvas.delete("image")
        return canvas.create_image(x,y,image=self.img,tag="image")
         
    def _draw(self, canvas, options):
        if self.anchor == None:
            self.anchor = Point(0, 0) # FIX: center point on canvas
        p = self.anchor
        x,y = canvas.toScreen(p.x,p.y)
        self.imageCache[self.imageId] = self.img # save a reference  
        return canvas.create_image(x,y,image=self.img,tag="image")
    
    def _move(self, dx, dy):
        self.anchor.move(dx,dy)
        
    def undraw(self):
        del self.imageCache[self.imageId]  # allow gc of tk photoimage
        GraphicsObject.undraw(self)

    def getAnchor(self):
        return self.anchor.clone()
            
    def clone(self):
        imgCopy = Pixmap(_tkCall(self.img.copy))
        other = Image(self.anchor, imgCopy)
        other.config = self.config.copy()
        return other

class Pixmap:
    """Pixmap represents an image as a 2D array of color values.
    A Pixmap can be made from a file (gif or ppm):

       pic = Pixmap("myPicture.gif")
       
    or initialized to a given size (initially transparent):

       pic = Pixmap(512, 512)


    """

    def __init__(self, *args):
        if len(args) == 1: # a file name or pixmap
            if type(args[0]) == type(""):
                self.image = _tkCall(tk.PhotoImage, file=args[0], master=_root)
            else:
                self.image = args[0]
        else: # arguments are width and height
            width, height = args
            self.image = _tkCall(tk.PhotoImage, master=_root,
                                width=width, height=height)
    
    def getWidth(self):
        """Returns the width of the image in pixels"""
        return _tkCall(self.image.width)

    def getHeight(self):
        """Returns the height of the image in pixels"""
        return _tkCall(self.image.height)

    def getPixel(self, x, y):
        """Returns a list [r,g,b] with the RGB color values for pixel (x,y)
        r,g,b are in range(256)

        """
        
        value = _tkCall(self.image.get, x,y)
        if type(value) ==  int:
            return [value, value, value]
        else:
            return map(int, value.split()) 

    def setPixel(self, x, y, (r,g,b)):
        """Sets pixel (x,y) to the color given by RGB values r, g, and b.
        r,g,b should be in range(256)

        """
        
        _tkExec(self.image.put, "{%s}"%color_rgb(r,g,b), (x, y))

    def clone(self):
        """Returns a copy of this Pixmap"""
        return Pixmap(self.image.copy())

    def save(self, filename):
        """Saves the pixmap image to filename.
        The format for the save image is determined from the filname extension.

        """
        
        path, name = os.path.split(filename)
        ext = name.split(".")[-1]
        _tkExec(self.image.write, filename, format=ext)
        
def color_rgb(r,g,b):
    """r,g,b are intensities of red, green, and blue in range(256)
    Returns color specifier string for the resulting color"""
    return "#%02x%02x%02x" % (r,g,b)

black     = Color(  0,   0,   0)
white     = Color(255, 255, 255)
blue      = Color(  0,   0, 255)
red       = Color(255,   0,   0)
green     = Color(  0, 255,   0)
gray      = Color(128, 128, 128)
darkGray  = Color( 64,  64,  64)
lightGray = Color(192, 192, 192)
yellow    = Color(255, 255,   0)
pink      = Color(255, 175, 175)
magenta   = Color(255,   0, 255)
cyan      = Color(  0, 255, 255)

def makeWindow(*args, **kwargs):
    return GraphWin(*args, **kwargs)

def makeImage(*args, **kwargs):
    return Image(*args, **kwargs)

def makeEntry(*args, **kwargs):
    return Entry(*args, **kwargs)

def makePoint(*args, **kwargs):
    return Point(*args, **kwargs)

def makeRectangle(*args, **kwargs):
    return Rectangle(*args, **kwargs)

def makeOval(*args, **kwargs):
    return Oval(*args, **kwargs)

def makeCircle(*args, **kwargs):
    return Circle(*args, **kwargs)

def makeLine(*args, **kwargs):
    return Line(*args, **kwargs)

def makePolygon(*args, **kwargs):
    return Polygon(*args, **kwargs)

def makeText(*args, **kwargs):
    return Text(*args, **kwargs)


class Sound:
    def __init__(self, filename):
        self.filename = filename
        if not myro.globvars.sound:
            _tkExec(tkSnack.initializeSnack, _root)
            myro.globvars.sound = 1
    def play(self):
        return _tkCall(self._play)
    def _play(self):
        self.snd = tkSnack.Sound()
        self.snd.read(self.filename)
        self.snd.play()
        return 

def makeSound(filename):
    return Sound(filename)

def play(sound):
    sound.play()

def _beep(duration, frequency1, frequency2):
    if tkSnack != None:
        if not myro.globvars.sound:
            tkSnack.initializeSnack(_root)
            myro.globvars.sound = 1
        snd1 = tkSnack.Sound()
        filt1 = tkSnack.Filter('generator', frequency1, 30000,
                               0.0, 'sine', int(11500*duration))
        if frequency2 != None:
            snd2 = tkSnack.Sound()
            filt2 = tkSnack.Filter('generator', frequency2, 30000,
                                   0.0, 'sine', int(11500*duration))
            map2 = tkSnack.Filter('map', 1.0)
            snd2.stop()
            # blocking is choppy; sleep below
            snd2.play(filter=filt2, blocking=0) 
        snd1.stop()
        # blocking is choppy; sleep below
        map1 = tkSnack.Filter('map', 1.0)
        snd1.play(filter=filt1, blocking=0)
        start = time.time()
        while time.time() - start < duration:
            myro.globvars.gui.update()
            time.sleep(.001)
    elif Tkinter != None:
        myro.globvars.gui.bell()            
        time.sleep(duration)
    else:
        time.sleep(duration)
    time.sleep(.1) # simulated delay, like real robot


class Joystick(Tkinter.Toplevel):
    def __init__(self, robot = None, showSensors = 0):
        _tkCall(self.__init_help, _root, robot, showSensors)

    def __init_help(self, parent = None, robot = None, showSensors = 0):
        Tkinter.Toplevel.__init__(self, parent)
        self.debug = 0
        self._running = 0
        self.robot = robot
        self.showSensors = showSensors
        self.parent = parent
        self.wm_title('Joystick')
        moveToTop(self)
        self.protocol('WM_DELETE_WINDOW',self.destroy)
        self.frame = Tkinter.Frame(self)
        label = Tkinter.Label(self.frame, text = "Forward")
        label.pack(side = "top")
        label = Tkinter.Label(self.frame, text = "Reverse")
        label.pack(side = "bottom")
        label = Tkinter.Label(self.frame, text = "Turn\nLeft")
        label.pack(side = "left")
        label = Tkinter.Label(self.frame, text = "Turn\nRight")
        label.pack(side = "right")
        self.canvas = Tkinter.Canvas(self.frame,
                                              width = 220,
                                              height = 220,
                                              bg = 'white')
        self.widgets = {}
        if self.showSensors:
            newFrame = Tkinter.Frame(self, relief=Tkinter.RAISED, borderwidth=2)
            items = []
            if self.robot != None:
                 d = self.robot.get("config")
                 items = [(key, d[key]) for key in d.keys()]
            self.addWidgets(newFrame, *items)
            newFrame.pack(side="bottom", fill="both", expand="y")
        self.initHandlers()
        self.canvas.pack(side=Tkinter.BOTTOM)

        self.circle_dim = (10, 10, 210, 210) #x0, y0, x1, y1
        self.circle = self.canvas.create_oval(self.circle_dim, fill = 'white')
        self.canvas.create_oval(105, 105, 115, 115, fill='black')

        self.frame.pack()
        self.translate = 0.0
        self.rotate = 0.0
        self.threshold = 0.10
        self.delay = 0.10 # in seconds
        self.running = 1
        self.after(250, self._update_help)

    def _update_help(self, delay = None):
        if self.robot and self.showSensors:
            config = self.robot.get("config")
            data = self.robot.getAll()
            for key in config:
                 item = data.get(key, [0] * config[key])
                 if type(item) not in [list, tuple]:
                      item = [item]
                 for i in range(len(item)):
                     self.updateWidget(key, i, item[i])
        self.update()
        if self.running:
            self.after(250, self._update_help)

    def destroy(self):
        self.running = 0
        if self.robot != None:
            self.robot.lock.acquire()
        Tkinter.Toplevel.destroy(self)
        if self.robot != None:
            self.robot.lock.release()

    def addWidgets(self, window, *items):
        for name, size in items:
            text = name + ":"
            frame = Tkinter.Frame(window)
            self.widgets[name + ".label"] = Tkinter.Label(frame, text=text, width=10)
            self.widgets[name + ".label"].pack(side="left")
            for i in range(size - 1, -1, -1):
                self.widgets["%s%d.entry" % (name, i)] = Tkinter.Entry(frame, bg="white", width = 10)
                self.widgets["%s%d.entry" % (name, i)].insert(0, "")
                self.widgets["%s%d.entry" % (name, i)].pack(side="right", fill="both", expand="y")
            frame.pack(side="bottom", fill="both", expand="y")

    def updateWidget(self, name, pos, value):
        """Updates the device view window."""
        if not self.showSensors: return
        try:
             self.widgets["%s%d.entry" % (name, pos)].delete(0,'end')
             self.widgets["%s%d.entry" % (name, pos)].insert(0,str(value))
        except: pass

    def minorloop(self, delay = None): # in milliseconds
        """
        As opposed to mainloop. This is a simple loop that works
        in IDLE.
        """
        if delay != None:
            self.delay = delay
        self.running = 1
        lastUpdated = 0
        lastData = []
        config = self.robot.get("config") # {"ir": 2, "line": 2, "stall": 1, "light": 3}
        while self.running:
            #self.focus_set()
            if self.robot and self.showSensors:
                  data = self.robot.getLastSensors()
                  now = time.time()
                  if data != lastData or now - lastUpdated > 1:
                        if now - lastUpdated > 1:
                             data = self.robot.getAll()
                        for key in config:
                             item = data.get(key, [0] * config[key])
                             if type(item) not in [list, tuple]:
                                  item = [item]
                             for i in range(len(item)):
                                 self.updateWidget(key, i, item[i])
                        lastUpdated = time.time()
                        lastData = data
            self.update()
            time.sleep(self.delay)

    def initHandlers(self):
        self.canvas.bind("<ButtonRelease-1>", self.canvas_clicked_up)
        self.canvas.bind("<Button-1>", self.canvas_clicked_down)
        self.canvas.bind("<B1-Motion>", self.canvas_moved)

    def getValue(self, event = None):
        return self.translate, self.rotate

    def move(self, translate, rotate):
        self.translate = translate
        if self.translate < 0.0:
            self.translate += self.threshold
        elif self.translate > 0.0:
            self.translate -= self.threshold
        self.rotate = rotate
        if self.rotate < 0.0:
            self.rotate += self.threshold
        elif self.rotate > 0.0:
            self.rotate -= self.threshold
        if self.debug:
            print self.translate, self.rotate
        if self.robot != None:
            #self.robot.lock.acquire()
            self.robot.move(self.translate, self.rotate)
            #self.robot.lock.release()

    def canvas_clicked_up(self, event):
        self.canvas.delete("lines")
        self.move(0.0, 0.0)

    def drawArrows(self, x, y, trans, rotate):
        if trans == 0:
            self.canvas.create_line(110, 110, 110, y, width=3, fill="blue", tag="lines")
        else:
            self.canvas.create_line(110, 110, 110, y, width=3, fill="blue", tag="lines", arrowshape = (10, 10, 3), arrow = "last")
        if rotate == 0:
            self.canvas.create_line(110, 110, x, 110, width=3, fill="red", tag="lines")
        else:
            self.canvas.create_line(110, 110, x, 110, width=3, fill="red", tag="lines", arrowshape = (10, 10, 3), arrow = "last")

    def canvas_clicked_down(self, event):
        if self.in_circle(event.x, event.y):
            trans, rotate = self.calc_tr(event.x, event.y)
            self.drawArrows(event.x, event.y, trans, rotate)
            self.move(trans, rotate)

    def canvas_moved(self, event):
        if self.in_circle(event.x, event.y):
            self.canvas.delete("lines")
            trans, rotate = self.calc_tr(event.x, event.y)
            self.drawArrows(event.x, event.y, trans, rotate)            
            self.move(trans, rotate)

    def stop(self):
        self.move(0.0, 0.0)

    def in_circle(self, x, y):
        r2 = ((self.circle_dim[2] - self.circle_dim[0])/2)**2
              
        center = ((self.circle_dim[2] + self.circle_dim[0])/2,
                     (self.circle_dim[3] + self.circle_dim[1])/2)
        #x in?
        dist2 = (center[0] - x)**2 + (center[1] - y)**2
        if (dist2 < r2):
            return 1
        else:
            return 0

    def calc_tr(self, x, y):
        #right is negative
        center = ((self.circle_dim[2] + self.circle_dim[0])/2,
                     (self.circle_dim[3] + self.circle_dim[1])/2)
        rot = float(center[0] - x) / float(center[0] - self.circle_dim[0])
        trans = float(center[1] - y) / float(center[1] - self.circle_dim[1])
        if abs(rot) < self.threshold:
            rot = 0.0
        if abs(trans) < self.threshold:
            trans = 0.0
        return (trans, rot)


class Calibrate(Tkinter.Toplevel):
    def __init__(self, robot = None):
        _tkCall(self.__init_help, _root, robot)

    def __init_help(self, parent = None, robot = None):
        Tkinter.Toplevel.__init__(self, parent)
        self.debug = 0
        self._running = 0

        self.robot = robot
        
        self._f1,self._f2,self._f3,self._f4  = self.robot.getFudge()
        self._lastFudged = time.time()
        self.parent = parent
        self.wm_title('calibstick')
        moveToTop(self)
        self.protocol('WM_DELETE_WINDOW',self.destroy)
        self.frame = Tkinter.Frame(self)
        label = Tkinter.Label(self.frame, text = "Forward")
        label.pack(side = "top")
        label = Tkinter.Label(self.frame, text = "Reverse")
        label.pack(side = "bottom")
        label = Tkinter.Label(self.frame, text = "Turn\nLeft")
        label.pack(side = "left")
        label = Tkinter.Label(self.frame, text = "Turn\nRight")
        label.pack(side = "right")
        self.canvas = Tkinter.Canvas(self.frame,
                                              width = 220,
                                              height = 220,
                                              bg = 'white')
        self.widgets = {}
        newFrame = Tkinter.Frame(self, relief=Tkinter.RAISED, borderwidth=2)
        self.addWidgets(newFrame, ("-1 Tweak", 1), ("-0.5 Tweak", 1), ("0.5 Tweak", 1), ("1 Tweak", 1))
        newFrame.pack(side="bottom", fill="both", expand="y")


        self.initHandlers()
        self.canvas.pack(side=Tkinter.BOTTOM)

##        self.circle_dim = (10, 100, 210, 120) #x0, y0, x1, y1
##        
##        self.circle = self.canvas.create_rectangle(self.circle_dim, fill = 'white')
        
        #Create bars for the 1, 0.5, 0, -0.5, and -1 settings (200 x 20 rect)
        self.canvas.create_rectangle((10, 5, 210, 25), fill = 'white') #-1
        self.canvas.create_rectangle((10, 45, 210, 65), fill = 'white') #-0.5
        self.canvas.create_rectangle((10, 150, 210, 170), fill = 'white') #0.5
        self.canvas.create_rectangle((10, 195, 210, 215), fill = 'white') #1

        #Create some marks for the 1, 0.5, 0, -0.5, and 1 settings
        self.canvas.create_rectangle(105, 10,  115, 20, fill='black')
        self.canvas.create_rectangle(105, 50,    115, 60, fill = 'black')
        self.canvas.create_rectangle(105, 105, 115, 115, fill='black')
        self.canvas.create_rectangle(105, 155, 115, 165, fill='black')
        self.canvas.create_rectangle(105, 200, 115, 210, fill='black')
        

        self.frame.pack()
        self.translate = 0.0
        self.rotate = 0.0
        self.threshold = 0.00
        self.delay = 0.10 # in seconds
        self.running = 0

        self.updateWidget("1 Tweak",0,self._f1)
        self.updateWidget("0.5 Tweak",0,self._f2)
        self.updateWidget("-0.5 Tweak",0,self._f3)
        self.updateWidget("-1 Tweak",0,self._f4)

        #END OF THE FUNCTION!



    def destroy(self):
         self.running = 0
         Tkinter.Toplevel.destroy(self)

    def addWidgets(self, window, *items):
        for name, size in items:
            text = name + ":"
            frame = Tkinter.Frame(window)
            self.widgets[name + ".label"] = Tkinter.Label(frame, text=text, width=10)
            self.widgets[name + ".label"].pack(side="left")
            for i in range(size):
                self.widgets["%s%d.entry" % (name, i)] = Tkinter.Entry(frame, bg="white", width = 10)
                self.widgets["%s%d.entry" % (name, i)].insert(0, "")
                self.widgets["%s%d.entry" % (name, i)].pack(side="right", fill="both", expand="y")
            frame.pack(side="bottom", fill="both", expand="y")

    def updateWidget(self, name, pos, value):
        """Updates the device view window."""
        try:
             self.widgets["%s%d.entry" % (name, pos)].delete(0,'end')
             self.widgets["%s%d.entry" % (name, pos)].insert(0,value)
        except: pass

    def minorloop(self, delay = None): # in milliseconds
        """
        As opposed to mainloop. This is a simple loop that works
        in IDLE.
        """
        if delay != None:
             self.delay = delay
        self.running = 1
        lastUpdated = 0
        lastData = []
        while self.running:
             self.update()
             time.sleep(self.delay)

    def initHandlers(self):
        self.canvas.bind("<ButtonRelease-1>", self.canvas_clicked_up)
        self.canvas.bind("<Button-1>", self.canvas_clicked_down)
        self.canvas.bind("<B1-Motion>", self.canvas_moved)

    def getValue(self, event = None):
        return self.translate, self.rotate

    def move(self, translate, rotate):
        self.translate = translate
        if self.translate < 0.0:
            self.translate += self.threshold
        elif self.translate > 0.0:
            self.translate -= self.threshold
        self.rotate = rotate
        if self.rotate < 0.0:
            self.rotate += self.threshold
        elif self.rotate > 0.0:
            self.rotate -= self.threshold
        if self.debug:
            print self.translate, self.rotate
        if self.robot != None:
            #self.robot.lock.acquire()
            self.robot.move(self.translate, self.rotate)
            #self.robot.lock.release()

    def canvas_clicked_up(self, event):
        self.canvas.delete("lines")
        self.move(0.0, 0.0)

    def drawArrows(self, x, y, trans, rotate):
        if trans != 0:
            self.canvas.create_line(110, 110, 110,(110-(90*trans)) , width=3, fill="blue", tag="lines", arrowshape = (10, 10, 3), arrow = "last")
        self.canvas.create_line(110, 110, x, 110, width=3, fill="red", tag="lines", arrowshape = (10, 10, 3), arrow = "last")

    def canvas_clicked_down(self, event):
        trans, rotate = self.calc_tr(event.x, event.y)
        self.drawArrows(event.x, event.y, trans, rotate)
        self.move(trans, rotate)

    def canvas_moved(self, event):
        self.canvas.delete("lines")
        trans, rotate = self.calc_tr(event.x, event.y)
        self.drawArrows(event.x, event.y, trans, rotate)            
        self.move(trans, rotate)

    def stop(self):
        self.move(0.0, 0.0)

    def calc_tr(self, x, y):
        offCenter = (x - 105.0) / 205.0

        if (offCenter > 1.0):
            offCenter = 1.0

        if (offCenter < -1.0):
            offCenter = -1.0

        if ( y < 35):
            speed = 1.0
            self._f1 = (1.0 - offCenter)
        elif ( y < 82):
            speed = 0.5
            self._f2 = (1.0 - offCenter)
        elif (y < 135):
            speed = 0.0

        elif (y < 182):
            speed = -0.5
            self._f3 = (1.0 - offCenter)
        else:
            speed = -1.0
            self._f4 = (1.0 - offCenter)

        self.updateWidget("1 Tweak",0,self._f1)
        self.updateWidget("0.5 Tweak",0,self._f2)
        self.updateWidget("-0.5 Tweak",0,self._f3)
        self.updateWidget("-1 Tweak",0,self._f4)
        #Update the fudge values.
        self.robot.setFudge(self._f1,self._f2,self._f3,self._f4)

        trans = speed
        rot = 0.0
        return (trans, rot)

# Kill the tk thread at exit
if myro.globvars.runtkthread:
    atexit.register(_tkShutdown)

