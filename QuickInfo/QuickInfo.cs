using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace VSIX_Test
{
    // The QuickInfo Source is responsible for collecting the set of identifiers and their descriptions,
    // and adding the content to the tooltip text buffer when one of the identifiers is encountered.
    //
    // Here we "load" them in the constructor, but ideally they are queried at runtime whenever the trigger starts
    // as we might have too many suggestions to show at once
    internal class TestQuickInfoSource : IAsyncQuickInfoSource
    {
        #region Disposable Pattern
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            System.GC.SuppressFinalize(this);
        }
        #endregion

        private readonly TestQuickInfoSourceProvider m_provider;
        private readonly ITextBuffer m_subjectBuffer;
        private readonly Dictionary<string, string> m_dictionary;

        private readonly QuickInfoItem EmptyItem = new QuickInfoItem(null, "");

        public TestQuickInfoSource(TestQuickInfoSourceProvider provider, ITextBuffer subjectBuffer)
        {
            VSIX_TestPackage.logger.Info("TestQuickInfoSource");

            m_provider = provider;
            m_subjectBuffer = subjectBuffer;

            //these are the method names and their descriptions
            m_dictionary = new Dictionary<string, string>
            {
                { "add", "addition" },
                { "int", "integer" },
                { "cpp", "love/hate relationship" }
            };
        }

        // Here we return the tooltip text depending on the snapshot text
        // If you don't want to display anything, create a QuickInfoItem that has a null ITrackingSpan and an empty string as the tooltip
        public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            // Map the trigger point down to our buffer.
            SnapshotPoint? subjectTriggerPoint = session.GetTriggerPoint(m_subjectBuffer.CurrentSnapshot);
            if (!subjectTriggerPoint.HasValue)
            {
                return Task.FromResult(EmptyItem);
            }

            ITextSnapshot currentSnapshot = subjectTriggerPoint.Value.Snapshot;

            //look for occurrences of our QuickInfo words in the span
            ITextStructureNavigator navigator = m_provider.NavigatorService.GetTextStructureNavigator(m_subjectBuffer);
            TextExtent extent = navigator.GetExtentOfWord(subjectTriggerPoint.Value);
            string searchText = extent.Span.GetText();

            foreach (string key in m_dictionary.Keys)
            {
                // In the text we are hovering, did we find one of our keywords?
                int foundIndex = searchText.IndexOf(key, StringComparison.CurrentCultureIgnoreCase);
                if (foundIndex > -1)
                {
                    ITrackingSpan applicableToSpan = currentSnapshot.CreateTrackingSpan
                    (
                        extent.Span.Start + foundIndex, key.Length, SpanTrackingMode.EdgeInclusive
                    );

                    if (m_dictionary.TryGetValue(key, out string value))
                        return Task.FromResult(new QuickInfoItem(applicableToSpan, value));
                }
            }

            return Task.FromResult(EmptyItem);
        }
    }

    // The provider of the QuickInfo source serves primarily to export itself as a MEF component part and instantiate the QuickInfo source.
    // Here you can import other MEF components.
    // Mostly used to register our QuickInfo with specific content types
    // 
    // https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.language.intellisense.iasyncquickinfosourceprovider?view=visualstudiosdk-2019
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name("ToolTip QuickInfo Source")]
    [Order(Before = "Default Quick Info Presenter")]
    [ContentType("C/C++")]
    internal class TestQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        [Import]
        internal ITextBufferFactoryService TextBufferFactoryService { get; set; }

        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return new TestQuickInfoSource(this, textBuffer);
        }
    }

    // QuickInfo Controllers determine when QuickInfo is displayed.
    // In this example, QuickInfo appears when the pointer is over a word that corresponds to one of the method names.
    // The QuickInfo controller implements a mouse hover event handler that triggers a QuickInfo session.
    internal class TestQuickInfoController : IIntellisenseController
    {
        private ITextView m_textView;
        private readonly IList<ITextBuffer> m_subjectBuffers;
        private readonly TestQuickInfoControllerProvider m_provider;
        private IAsyncQuickInfoSession m_session;

        internal TestQuickInfoController(ITextView textView, IList<ITextBuffer> subjectBuffers, TestQuickInfoControllerProvider provider)
        {
            m_textView = textView;
            m_subjectBuffers = subjectBuffers;
            m_provider = provider;

            // Add additional events here
            m_textView.MouseHover += OnTextViewMouseHover;
        }

        // This must be Async because we are using an IAsyncQuickInfoBroker.
        // Won't compile without 'await' and deadlocks if other methods are used, like with a JoinableFactory
        private async void OnTextViewMouseHover(object sender, MouseHoverEventArgs e)
        {
            //find the mouse position by mapping down to the subject buffer
            SnapshotPoint? point = m_textView.BufferGraph.MapDownToFirstMatch
                 (new SnapshotPoint(m_textView.TextSnapshot, e.Position),
                PointTrackingMode.Positive,
                snapshot => m_subjectBuffers.Contains(snapshot.TextBuffer),
                PositionAffinity.Predecessor);

            if (point == null)
                return;

            ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(point.Value.Position,
            PointTrackingMode.Positive);

            if (!m_provider.QuickInfoBroker.IsQuickInfoActive(m_textView))
                m_session = await m_provider.QuickInfoBroker.TriggerQuickInfoAsync(m_textView, triggerPoint, QuickInfoSessionOptions.None);
        }

        // Called when an ITextBuffer is connected to the graph
        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer) { }

        // Called when the ITextView is removed from the graph
        public void Detach(ITextView textView)
        {
            if (m_textView == textView)
            {
                m_textView.MouseHover -= OnTextViewMouseHover;
                m_textView = null;
            }
        }

        // Called when a ITextBuffer is removed from the graph.
        // WARNING, it is not guaranteed that it's the same buffer we are connected to.
        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer) { }
    }

    // The provider of the QuickInfo controller serves primarily to export itself as a MEF component part and instantiate the QuickInfo controller.
    // Can also import additional MEF components.
    [Export(typeof(IIntellisenseControllerProvider))]
    [Name("ToolTip QuickInfo Controller")]
    [ContentType("C/C++")]
    internal class TestQuickInfoControllerProvider : IIntellisenseControllerProvider
    {
        [Import]
        internal IAsyncQuickInfoBroker QuickInfoBroker { get; set; }

        // Can return null if no controller can be created
        public IIntellisenseController TryCreateIntellisenseController(ITextView textView, IList<ITextBuffer> subjectBuffers)
        {
            return new TestQuickInfoController(textView, subjectBuffers, this);
        }
    }
}
