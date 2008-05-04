using Gtk;
using GtkSourceView;
using System.IO;
using System;

using PyjamaInterfaces;
using PyjamaGraphics;

public class TextDocument: PyjamaInterfaces.IDocument
{
    string filename;
    SourceBuffer buffer;
    SourceLanguage language;
    SourceView source_view;
    int page;
    bool dirty;
    MainWindow window;
    
    public TextDocument(MainWindow win, string fn, int page)
    {
	window = win;
        filename = fn;
	this.page = page;
	string mime_type = GetMimeType(filename);
        SourceLanguagesManager mgr = new SourceLanguagesManager();
	language = mgr.GetLanguageFromMimeType(mime_type);
        // Set up syntax highlighting
	buffer = new SourceBuffer(language);
        buffer.Highlight = true;
	source_view = new SourceView();
	source_view.Buffer = buffer;
	source_view.Buffer.Changed += new EventHandler(OnSourceViewChanged);
	// Options should be set by user:
	source_view.WrapMode = Gtk.WrapMode.Word;
        source_view.ShowLineNumbers = true;
        source_view.AutoIndent = true;
	if (filename != null) {
	    // TODO: make sure it exists, and can be read; readonly?
	    StreamReader file = File.OpenText(fn);
	    buffer.Text = file.ReadToEnd();
	    buffer.PlaceCursor(buffer.StartIter);
	} else {
	    filename = Utils.Tran("Untitled") + "-" + this.page + ".py";
	}
    }

    private void OnSourceViewChanged(object obj, EventArgs args) 
    {
	if (!dirty) {
	    Console.WriteLine("callback");
	    window.SetDirty(page, true);
	}
	dirty = true;
    }
    
    public Widget GetView()
    {
	return (Widget) source_view;
    }

    public string GetShortName()
    {
	// Path.GetDirectoryName()
	string name = System.IO.Path.GetFileName(filename);
	if (name == "__init__") {
	    // add directory onto name
	}
	return name;
    }
    
    public bool GetModified()
    {
	return buffer.Modified;
    }
        
    public void SetModified(bool value)
    {
	buffer.Modified = value;
    }
    
    public void Save()
    {
        StreamWriter file = new StreamWriter(filename);
        file.Write(buffer.Text);
        file.Close();
	dirty = false;
	window.SetDirty(page, false);
    }
    
    public void SaveAs(string fn)
    {
    	filename = fn;
	string mime_type = GetMimeType(filename);
	SourceLanguagesManager mgr = new SourceLanguagesManager();
	language = mgr.GetLanguageFromMimeType(mime_type);
	if (buffer.Language != language) {
	    buffer.Language = language;
	}
        Save();
    }

    public void SetFilename(string fn)
    {
    	filename = fn;
    }

    public string GetFilename()
    {
    	return filename;
    }

    string GetMimeType(string filename) {
	if (filename != null) {
	    string extension = System.IO.Path.GetExtension(filename);
	    return Utils.GetMimeType(extension);
	} else {
	    return "text/x-python";
	}
    }
}

