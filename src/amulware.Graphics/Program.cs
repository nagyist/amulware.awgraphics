﻿using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using OpenTK.Platform;

namespace amulware.Graphics
{
    public class Program : NativeWindow, IGameWindow, IDisposable
    {
        #region --- Fields ---

        object exit_lock = new object();

        IGraphicsContext glContext;

        bool isExiting = false;

        private int next_render_time = 0;
        private int update_time_interval;
        private int render_time_interval;

        int frames_last_second = 0;

        #endregion

        #region --- Contructors ---


        public Program()
            : this(640, 480, GraphicsMode.Default, "OpenTK Game Window", 0, DisplayDevice.Default) { }

        public Program(int width, int height)
            : this(width, height, GraphicsMode.Default, "OpenTK Game Window", 0, DisplayDevice.Default) { }

        public Program(int width, int height, GraphicsMode mode)
            : this(width, height, mode, "OpenTK Game Window", 0, DisplayDevice.Default) { }

        public Program(int width, int height, GraphicsMode mode, string title)
            : this(width, height, mode, title, 0, DisplayDevice.Default) { }

        public Program(int width, int height, GraphicsMode mode, string title, GameWindowFlags options)
            : this(width, height, mode, title, options, DisplayDevice.Default) { }

        public Program(int width, int height, GraphicsMode mode, string title, GameWindowFlags options, DisplayDevice device)
            : this(width, height, mode, title, options, device, 1, 0, GraphicsContextFlags.Default)
        { }

        public Program(int width, int height, GraphicsMode mode, string title, GameWindowFlags options, DisplayDevice device,
            int major, int minor, GraphicsContextFlags flags)
            : this(width, height, mode, title, options, device, major, minor, flags, null)
        { }

        public Program(int width, int height, GraphicsMode mode, string title, GameWindowFlags options, DisplayDevice device,
                          int major, int minor, GraphicsContextFlags flags, IGraphicsContext sharedContext)
            : base(width, height, title, options,
                   mode == null ? GraphicsMode.Default : mode,
                   device == null ? DisplayDevice.Default : device)
        {
            try
            {
                glContext = new GraphicsContext(mode == null ? GraphicsMode.Default : mode, WindowInfo, major, minor, flags);
                glContext.MakeCurrent(WindowInfo);
                (glContext as IGraphicsContextInternal).LoadAll();
            }
            catch (Exception e)
            {
                Debug.Print(e.ToString());
                base.Dispose();
                throw;
            }
            this.Context.VSync = false; // disable vsync
        }

        #endregion

        #region --- Public Members ---

        #region Methods

        #region Dispose

        /// <summary>
        /// Disposes of the Program, releasing all resources consumed by it.
        /// </summary>
        public override void Dispose()
        {
            try
            {
                Dispose(true);
            }
            finally
            {
                try
                {
                    if (glContext != null)
                    {
                        glContext.Dispose();
                        glContext = null;
                    }
                }
                finally
                {
                    base.Dispose();
                }
            }
            GC.SuppressFinalize(this);
        }

        #endregion

        #region MakeCurrent

        /// <summary>
        /// Makes the GraphicsContext current on the calling thread.
        /// </summary>
        public void MakeCurrent()
        {
            EnsureUndisposed();
            Context.MakeCurrent(WindowInfo);
        }

        #endregion

        #region OnClose

        /// <summary>
        /// Called when the NativeWindow is about to close.
        /// </summary>
        /// <param name="e">
        /// The <see cref="System.ComponentModel.CancelEventArgs" /> for this event.
        /// Set e.Cancel to true in order to stop the Program from closing.</param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!e.Cancel)
            {
                isExiting = true;
                OnUnloadInternal(EventArgs.Empty);
            }
        }


        #endregion

        #region OnLoad

        /// <summary>
        /// Called after an OpenGL context has been established, but before entering the main loop.
        /// </summary>
        /// <param name="e">Not used.</param>
        protected virtual void OnLoad(EventArgs e)
        {
            if (Load != null) Load(this, e);
        }

        #endregion

        #region OnUnload

        /// <summary>
        /// Called after Program.Exit was called, but before destroying the OpenGL context.
        /// </summary>
        /// <param name="e">Not used.</param>
        protected virtual void OnUnload(EventArgs e)
        {
            if (Unload != null) Unload(this, e);
        }

        #endregion
        
        public void Run()
        {
            Run(0.0, 0.0);
        }

        public void Run(double updateRate)
        {
            Run(updateRate, 0.0);
        }

        public void Run(double updates_per_second, double frames_per_second)
        {
            EnsureUndisposed();

            Visible = true;   // Make sure the Program is visible.
            OnLoadInternal(EventArgs.Empty);
            OnResize(EventArgs.Empty);

            this.update_time_interval = 0;
            if (updates_per_second > 0 && updates_per_second <= 1000)
                this.update_time_interval = (int)(1000 / updates_per_second);
            this.render_time_interval = 0;
            if (frames_per_second > 0 && frames_per_second <= 1000)
                this.render_time_interval = (int)(1000 / frames_per_second);

            int startTime = System.Environment.TickCount;
            int lastUpdateTime = startTime;


            int next_fps_mark = 0;
            int frames_this_second = 0;

            next_fps_mark = startTime;
            
            UpdateEventArgs updateEventArgs = new UpdateEventArgs(startTime);

            int fakeCurrentTime = startTime;

            // main loop
            while (true)
            {
                int currentTime = System.Environment.TickCount;
                frames_this_second++;

                fakeCurrentTime += this.update_time_interval;

                ProcessEvents();
                if (Exists && !IsExiting)
                {
                    // update
                    updateEventArgs = new UpdateEventArgs(updateEventArgs, fakeCurrentTime);

                    OnUpdate(updateEventArgs);

                    if (currentTime >= this.next_render_time)
                    {
                        // render
                        OnRender(updateEventArgs);
                        this.next_render_time = currentTime + this.render_time_interval;
                    }
                }
                else
                    return;

                int timeAfterFrame = System.Environment.TickCount;
                if (timeAfterFrame >= next_fps_mark)
                {
                    frames_last_second = frames_this_second;
                    frames_this_second = 0;
                    next_fps_mark = timeAfterFrame + 1000;
                    //this.Title = this.frames_last_second.ToString();
                }
                int updateTime = timeAfterFrame - currentTime;
                int waitTime = this.update_time_interval - updateTime;
                if (waitTime > 0)
                    Thread.Sleep(waitTime);
            }
        }
        
        protected virtual void OnUpdate(UpdateEventArgs e) { }
        protected virtual void OnRender(UpdateEventArgs e) { }


        public void SwapBuffers()
        {
            EnsureUndisposed();
            this.Context.SwapBuffers();
        }

        #region Properties

        #region Context

        /// <summary>
        /// Returns the opengl IGraphicsContext associated with the current Program.
        /// </summary>
        public IGraphicsContext Context
        {
            get
            {
                EnsureUndisposed();
                return glContext;
            }
        }

        #endregion

        #region IsExiting

        /// <summary>
        /// Gets a value indicating whether the shutdown sequence has been initiated
        /// for this window, by calling Program.Exit() or hitting the 'close' button.
        /// If this property is true, it is no longer safe to use any OpenTK.Input or
        /// OpenTK.Graphics.OpenGL functions or properties.
        /// </summary>
        public bool IsExiting
        {
            get
            {
                EnsureUndisposed();
                return isExiting;
            }
        }

        #endregion

        #region Joysticks
        
        /// <summary>
        /// Gets a readonly IList containing all available OpenTK.Input.JoystickDevices.
        /// </summary>
        public IList<JoystickDevice> Joysticks
        {
            get { return InputDriver.Joysticks; }
        }
        
        #endregion

        #region Keyboard

        /// <summary>
        /// Gets the primary Keyboard device, or null if no Keyboard exists.
        /// </summary>
        public KeyboardDevice Keyboard
        {
            get { return InputDriver.Keyboard.Count > 0 ? InputDriver.Keyboard[0] : null; }
        }

        #endregion

        #region Mouse

        /// <summary>
        /// Gets the primary Mouse device, or null if no Mouse exists.
        /// </summary>
        public MouseDevice Mouse
        {
            get { return InputDriver.Mouse.Count > 0 ? InputDriver.Mouse[0] : null; }
        }

        #endregion


        #region WindowState

        /// <summary>
        /// Gets or states the state of the NativeWindow.
        /// </summary>
        public override WindowState WindowState
        {
            get
            {
                return base.WindowState;
            }
            set
            {
                base.WindowState = value;
                Debug.Print("Updating Context after setting WindowState to {0}", value);

                if (Context != null)
                    Context.Update(WindowInfo);
            }
        }
        #endregion

        #endregion

        #region Events

        /// <summary>
        /// Occurs before the window is displayed for the first time.
        /// </summary>
        public event EventHandler<EventArgs> Load;

        /// <summary>
        /// Occurs when it is time to render a frame.
        /// </summary>
        public event EventHandler<FrameEventArgs> RenderFrame;

        /// <summary>
        /// Occurs before the window is destroyed.
        /// </summary>
        public event EventHandler<EventArgs> Unload;

        /// <summary>
        /// Occurs when it is time to update a frame.
        /// </summary>
        public event EventHandler<FrameEventArgs> UpdateFrame;

        #endregion

        #endregion

        #region --- Protected Members ---

        #region Dispose

        /// <summary>
        /// Override to add custom cleanup logic.
        /// </summary>
        /// <param name="manual">True, if this method was called by the application; false if this was called by the finalizer thread.</param>
        protected virtual void Dispose(bool manual) { }

        #endregion

        #region OnRenderFrame

        /// <summary>
        /// Called when the frame is rendered.
        /// </summary>
        /// <param name="e">Contains information necessary for frame rendering.</param>
        /// <remarks>
        /// Subscribe to the <see cref="RenderFrame"/> event instead of overriding this method.
        /// </remarks>
        protected virtual void OnRenderFrame(FrameEventArgs e)
        {
            if (RenderFrame != null) RenderFrame(this, e);
        }

        #endregion

        #region OnUpdateFrame

        /// <summary>
        /// Called when the frame is updated.
        /// </summary>
        /// <param name="e">Contains information necessary for frame updating.</param>
        /// <remarks>
        /// Subscribe to the <see cref="UpdateFrame"/> event instead of overriding this method.
        /// </remarks>
        protected virtual void OnUpdateFrame(FrameEventArgs e)
        {
            if (UpdateFrame != null) UpdateFrame(this, e);
        }

        #endregion

        #region OnWindowInfoChanged

        /// <summary>
        /// Called when the WindowInfo for this Program has changed.
        /// </summary>
        /// <param name="e">Not used.</param>
        protected virtual void OnWindowInfoChanged(EventArgs e) { }

        #endregion

        #region OnResize

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            glContext.Update(base.WindowInfo);
        }

        #endregion

        #endregion

        #region --- Private Members ---

        #region OnLoadInternal

        private void OnLoadInternal(EventArgs e)
        {
            OnLoad(e);
        }

        #endregion

        #region OnRenderFrameInternal

        private void OnRenderFrameInternal(FrameEventArgs e) { if (Exists && !isExiting) OnRenderFrame(e); }

        #endregion

        #region OnUnloadInternal

        private void OnUnloadInternal(EventArgs e) { OnUnload(e); }

        #endregion

        #region OnUpdateFrameInternal

        private void OnUpdateFrameInternal(FrameEventArgs e) { if (Exists && !isExiting) OnUpdateFrame(e); }

        #endregion

        #region OnWindowInfoChangedInternal

        private void OnWindowInfoChangedInternal(EventArgs e)
        {
            glContext.MakeCurrent(WindowInfo);

            OnWindowInfoChanged(e);
        }

        #endregion

        #endregion
        #endregion
        
    }
}