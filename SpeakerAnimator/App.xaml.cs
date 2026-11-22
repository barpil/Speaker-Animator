using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using SpeakerAnimator.Model.Mouth.BlendShapes;
using SpeakerAnimator.Model.NN;
using SpeakerAnimator.View;
using SpeakerAnimator.View.Panels;

namespace SpeakerAnimator;


public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string modelDir = Path.Combine(baseDir, "Data", "NN");

        var predictor = new VisemeOnnxPredictor(
            modelDir,
            preferGpu: true
        );

        AnimationController.Instantiate(predictor);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AnimationController.Instance?.Shutdown();
        base.OnExit(e);
    }
    
}