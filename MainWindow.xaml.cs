using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;

namespace KinectDrawDotsGame
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private KinectSensor kinectDevice;
        private Skeleton[] frameSkeletons;
        private Point lastPoint;
        private String resultFile = "d:\\result.txt";
        private String depthResultFile = "d:\\depthResult.txt";

        public KinectSensor KinectDevice
        {
            get { return this.kinectDevice; }
            set
            {
                if (this.kinectDevice != value)
                {
                    //Uninitialize
                    if (this.kinectDevice != null)
                    {
                        this.kinectDevice.Stop();
                        this.kinectDevice.SkeletonFrameReady -= KinectDevice_SkeletonFrameReady;
                        this.kinectDevice.SkeletonStream.Disable();
                        this.frameSkeletons = null;
                    }

                    this.kinectDevice = value;

                    //Initialize
                    if (this.kinectDevice != null)
                    {
                        if (this.kinectDevice.Status == KinectStatus.Connected)
                        {
                            this.kinectDevice.SkeletonStream.Enable();
                            this.frameSkeletons = new Skeleton[this.kinectDevice.SkeletonStream.FrameSkeletonArrayLength];
                            // delete the last time point info file
                            if (File.Exists(resultFile))
                                File.Delete(resultFile);
                            if (File.Exists(depthResultFile))
                                File.Delete(depthResultFile);
                            this.kinectDevice.Start();
                            this.KinectDevice.SkeletonFrameReady += KinectDevice_SkeletonFrameReady;
                        }
                    }
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += (s, e) =>
            {
                KinectSensor.KinectSensors.StatusChanged += KinectSensors_StatusChanged;
                this.KinectDevice = KinectSensor.KinectSensors.FirstOrDefault(x => x.Status == KinectStatus.Connected);
            };
        }

        private void KinectSensors_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case KinectStatus.Initializing:
                case KinectStatus.Connected:
                case KinectStatus.NotPowered:
                case KinectStatus.NotReady:
                case KinectStatus.DeviceNotGenuine:
                    this.KinectDevice = e.Sensor;
                    break;
                case KinectStatus.Disconnected:
                    //TODO: Give the user feedback to plug-in a Kinect device.                    
                    this.KinectDevice = null;
                    break;
                default:
                    //TODO: Show an error state
                    break;
            }
        }

        private void KinectDevice_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame frame = e.OpenSkeletonFrame())
            {
                if (frame != null)
                {
                    frame.CopySkeletonDataTo(this.frameSkeletons);
                    Skeleton skeleton = GetPrimarySkeleton(this.frameSkeletons);

                    if (skeleton == null)
                    {
                        HandCursorElement.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        Joint frontHand = GetFrontHand(skeleton);
                        if (frontHand == skeleton.Joints[JointType.HandRight]) 
                        {
                            TrackHand(frontHand);
                            TrackPuzzle(frontHand.Position);
                        }
                        // Joint primaryHand = skeleton.Joints[JointType.HandRight];
                        
                    }
                }
            }
        }

        private static Skeleton GetPrimarySkeleton(Skeleton[] skeletons)
        {
            Skeleton skeleton = null;

            if (skeletons != null)
            {
                //查找最近的游戏者
                for (int i = 0; i < skeletons.Length; i++)
                {
                    if (skeletons[i].TrackingState == SkeletonTrackingState.Tracked)
                    {
                        if (skeleton == null)
                        {
                            skeleton = skeletons[i];
                        }
                        else
                        {
                            if (skeleton.Position.Z > skeletons[i].Position.Z)
                            {
                                skeleton = skeletons[i];
                            }
                        }
                    }
                }
            }

            return skeleton;
        }

        private static Joint GetFrontHand(Skeleton skeleton)
        {
            Joint primaryHand = new Joint();

            if (skeleton != null)
            {
                primaryHand = skeleton.Joints[JointType.HandLeft];
                Joint righHand = skeleton.Joints[JointType.HandRight];


                if (righHand.TrackingState != JointTrackingState.NotTracked)
                {
                    if (primaryHand.TrackingState == JointTrackingState.NotTracked)
                    {
                        primaryHand = righHand;
                    }
                    else
                    {
                        if (primaryHand.Position.Z > righHand.Position.Z)
                        {
                            primaryHand = righHand;
                        }
                    }
                }
            }

            return primaryHand;
        }

        private void TrackHand(Joint hand)
        {
            if (hand.TrackingState == JointTrackingState.NotTracked)
            {
                HandCursorElement.Visibility = Visibility.Collapsed;
            }
            else
            {
                HandCursorElement.Visibility = Visibility.Visible;

                DepthImagePoint point = this.kinectDevice.CoordinateMapper.MapSkeletonPointToDepthPoint(hand.Position, this.kinectDevice.DepthStream.Format);
                point.X = (int)((point.X * LayoutRoot.ActualWidth / kinectDevice.DepthStream.FrameWidth) - (HandCursorElement.ActualWidth / 2.0));
                point.Y = (int)((point.Y * LayoutRoot.ActualHeight / kinectDevice.DepthStream.FrameHeight) - (HandCursorElement.ActualHeight / 2.0));

                Canvas.SetLeft(HandCursorElement, point.X);
                Canvas.SetTop(HandCursorElement, point.Y);

                if (hand.JointType == JointType.HandRight)
                {
                    HandCursorScale.ScaleX = 1;
                }
                else
                {
                    HandCursorScale.ScaleX = -1;
                }
            }
        }

        private void TrackPuzzle(SkeletonPoint position)
        {
            DepthImagePoint point = this.kinectDevice.CoordinateMapper.MapSkeletonPointToDepthPoint(position, this.kinectDevice.DepthStream.Format);
            point.X = (int)(point.X * LayoutRoot.ActualWidth / kinectDevice.DepthStream.FrameWidth);
            point.Y = (int)(point.Y * LayoutRoot.ActualHeight / kinectDevice.DepthStream.FrameHeight);
            Point handPoint = new Point(point.X, point.Y);

            WritePointToFile(position);
            WriteDepthPointToFile(point);

            if (lastPoint == null) 
            {
                lastPoint = handPoint;
                return;
            }

            Polyline line = new Polyline();
            line.Stroke = Brushes.SlateGray;
            line.StrokeThickness = 2;
            line.FillRule = FillRule.EvenOdd;
            PointCollection pointCollection = new PointCollection();
            pointCollection.Add(lastPoint);
            pointCollection.Add(handPoint);
            line.Points = pointCollection;
            PuzzleBoardElement.Children.Add(line);

            lastPoint = handPoint;
        }

        private void WritePointToFile(SkeletonPoint position)
        {
            using (StreamWriter file = new StreamWriter(resultFile, true))
            {
                file.WriteLine(position.X + " " + position.Y + " " + position.Z);
            }
        }

        private void WriteDepthPointToFile(DepthImagePoint depthPoint)
        {
            using (StreamWriter file = new StreamWriter(depthResultFile, true))
            {
                file.WriteLine(depthPoint.X + " " + depthPoint.Y + " " + depthPoint.Depth);
            }
        }

    }
}
