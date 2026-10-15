using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Diz.Controllers.controllers;
using Diz.Controllers.interfaces;
using Diz.Core.Interfaces;
using Diz.Core.model;
using Diz.Core.util;
using Diz.Ui.Winforms.dialogs;

namespace Diz.Ui.Winforms.window;

public partial class MainWindow
{
    // maybe rethink how document and project are interacted with.
    private IDizDocument Document { get; }
    
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Project Project
    {
        get => Document.Project;
        set => Document.Project = value;
    }

    // not sure if this will be the final place this lives. OK for now. -Dom
    public IProjectController ProjectController { get; }

    public ILongRunningTaskHandler.LongRunningTaskHandler TaskHandler => RunLongRunningTaskAsync;

    // new-ui plan step 6: replaces ProgressBarJob.RunAndWaitForCompletion (raw Thread + STA +
    // spin-wait on View.IsVisible()). Shows the progress window non-modally, runs the work on a
    // background Task, and closes the window when it finishes. Awaited from an async void UI
    // handler, so the WinForms message loop keeps pumping -- the window animates and the app
    // stays responsive with no worker thread poking the UI and no blocking ShowDialog().
    private async Task RunLongRunningTaskAsync(
        Action<IProgress<int>, CancellationToken> work, string description, bool isMarquee)
    {
        // No progress window: it popped up (and stole focus) on every save, which is unusable when
        // Diz lives on another desktop. The work still runs off the UI thread; progress reports go
        // to a no-op sink. description/isMarquee are kept in the signature for the other backends.
        //
        // this.Enabled = false blocks stray interaction with the main window while work runs;
        // re-enabled in finally so a throw in work() can't leave the app permanently disabled.
        this.Enabled = false;

        // cancellation is plumbed end-to-end but no cancel button is surfaced yet.
        using var cts = new CancellationTokenSource();
        try
        {
            await Task.Run(() => work(new Progress<int>(), cts.Token));
        }
        finally
        {
            this.Enabled = true;
        }
    }


    // sub windows
    private readonly ILabelEditorView labelsView;
    private readonly IRegionListView regionsView;
    private VisualizerForm? visualForm;

    // TODO: add a handler so we get notified when CurrentViewOffset changes.
    // then, we split most of our functions up into
    // 1. things that change ViewOffset
    // 2. things that react to ViewOffset changes.
    //
    // This will allow more flexibility and synchronizing different views (i.e. main table, graphics, layout, etc)
    // and this lets us save this value with the project file itself.

    // Data offset of the "view" i.e. the top of the table
    private int ViewOffset
    {
        get => Project?.ProjectUserSettings.CurrentViewOffset ?? 0;
        set => Project.ProjectUserSettings.CurrentViewOffset = value;
    }

    private bool importerMenuItemsEnabled;

    private Util.NumberBase displayBase = Util.NumberBase.Hexadecimal;
    private FlagType markFlag = FlagType.Data8Bit;
    
    private readonly IDizAppSettings appSettings;
    private readonly IAppVersionInfo appVersionInfo;
}