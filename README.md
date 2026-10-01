ImportXYZPointCloud.fsl - FLEXSIM USER LIBRARY
==============================

A FlexSim user library that adds a "ImportPointCloud" icon to the Library pane.
Dropping the icon into a model runs the tool's script.

For full details on what the tool does and how to use it, see the
documentation file: FlexSim_PointCloudImporter_UserGuide.pdf


REQUIREMENTS
------------
- FlexSim version 24.0 or later
- A model must be open when you run the tool


INSTALLATION
------------
1. Copy ImportXYZPointCloud.fsl to a folder on your computer.
2. In FlexSim, go to File > Open User Libraries... and select ImportXYZPointCloud.fsl.
3. If FlexSim asks whether to install the library's components, choose Yes.

The "ImportPointCloud" icon now appears at the top of the Library pane.

Load automatically on startup (optional):
1. Open File > Global Preferences.
2. Add ImportXYZPointCloud.fsl to the list of libraries loaded at startup.
3. Click OK and restart FlexSim.


USAGE
-----
1. Open your model.
2. Drag the "ImportPointCloud" icon from the Library pane into the 3D view.
3. The tool runs immediately. No object is added to the model.


CONTENTS
--------
ImportPointCloud   (Draggable icon)
              	    Runs the tool when dropped into a model.


TROUBLESHOOTING
---------------
Icon doesn't appear:
  Make sure the library is loaded under File > Open User Libraries...

Nothing happens on drop / "ImportPointCloud" not found:
  The User Command may not be installed. In the Library pane, click the
  arrow next to the library and choose "Install Library's Auto Install
  Components".

Errors in the Output Console:
  Check that your FlexSim version meets the requirement above.


VERSION HISTORY
---------------
1.0   10.1.2026   Initial release


CONTACT
-------
Simulation Bureau LLC - info@simulationbureau.com
