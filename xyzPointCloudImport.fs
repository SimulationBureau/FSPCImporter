/**PointCloud Import - Parts 1-4: folder, file selection, validation, PointCloud_N folder + copy, object, On Draw, mesh build*/

//FlexSim Point Cloud Importer V1.0
//Copyright (c) 2026 Simulation Bureau LLC
//Licensed under the MIT License.
//See the LICENSE file distributed with this software


// NOTE: fileexists() only detects FILES, not folders. So every folder this script
// creates gets a small marker file (folder_info.txt), and "does the folder exist?"
// is answered by checking for that marker file.

// ================= Settings =================
string folderName = "PointCloudFiles";
string namePrefix = "PointCloud_";
string fileFilter = "*.xyz";
string fileFilterDesc = "XYZ point cloud files";
string markerName = "folder_info.txt";   // marker file written into each folder the script creates
int maxTries = 200;                      // attempts to write the marker after creating a folder

int sampleLines = 10;                    // data rows checked during validation
int maxHeaderLines = 100;                // max header/comment lines allowed before the first data row
double fileUnitToMeters = 1.0;           // units of the .xyz file: 1.0 = m, 0.001 = mm, 0.01 = cm, 0.0254 = in, 0.3048 = ft
double colorScale = 255.0;               // file colors are 0-255; mesh colors are 0-1
double defaultGray = 0.7;                // color used when the file has no RGB columns
double pointSize = 4;                    // starting value of the object's PointSize label
int copySourceFile = 1;                  // 1 = copy the .xyz into PointCloudFiles\PointCloud_N\, 0 = don't copy
int showSummary = 1;                     // 1 = popup summary at the end, 0 = console only
int progressEvery = 250000;              // print progress every N points

// ---------- Unit conversion: file units -> model length units ----------
// getmodelunit(LENGTH_MULTIPLE) = size of one model length unit in meters (inches -> 0.0254)
double modelUnitToMeters = getmodelunit(LENGTH_MULTIPLE);
double coordScale = fileUnitToMeters / modelUnitToMeters;
print("Unit scale:      1 file unit = " + string.fromNum(coordScale, 6) + " model units" +
	" (file unit " + string.fromNum(fileUnitToMeters, 4) + " m, model unit " + string.fromNum(modelUnitToMeters, 4) + " m)");

// ================= PART 1: model folder and PointCloudFiles =================

// ---------- Step 1: model must be saved ----------
// modeldir() is empty for a model that has never been saved.
string modelFile = currentfile();
string modelDir = modeldir();

if (modelDir.length == 0) {
	msg("Point Cloud Import",
		"This model has not been saved yet.\n\n" +
		"Save the model first so the script knows where to create the " +
		folderName + " folder, then run the script again.");
	print("PointCloud Import stopped: model not saved.");
	return 0;
}

// Normalize to backslashes and make sure the directory ends with one
modelDir = modelDir.split("/").join("\\");
if (!modelDir.endsWith("\\"))
	modelDir += "\\";

print("Model file:      " + modelFile);
print("Model directory: " + modelDir);

// ---------- Step 2: find or create PointCloudFiles ----------
string folderPath = modelDir + folderName;
string folderMarker = folderPath + "\\" + markerName;

if (fileexists(folderMarker)) {
	print("Folder found:    " + folderPath);
} else {
	// createdirectory is harmless if the folder already exists
	applicationcommand("createdirectory", folderPath);

	// Write the marker; retry in case Windows is still finishing the folder
	int tries1 = 0;
	while (!fileexists(folderMarker) && tries1 < maxTries) {
		fileopen(folderMarker, "w");
		fpt("PointCloudFiles folder for model: " + modelFile);
		fpr();
		fileclose();
		tries1++;
	}

	if (fileexists(folderMarker)) {
		print("Folder ready:    " + folderPath);
	} else {
		msg("Point Cloud Import", "Could not create or write to folder:\n\n" + folderPath);
		print("PointCloud Import stopped: could not create " + folderPath);
		return 0;
	}
}

// ================= PART 2: select the .xyz file =================

// ---------- Step 3: let the user pick the file ----------
string selectedFile = filebrowse(fileFilter, fileFilterDesc, folderPath + "\\");

if (selectedFile.length == 0) {
	print("PointCloud Import cancelled: no file selected.");
	return 0;
}

if (!fileexists(selectedFile)) {
	msg("Point Cloud Import", "The selected file could not be found:\n\n" + selectedFile);
	print("PointCloud Import stopped: file not found: " + selectedFile);
	return 0;
}

// ---------- Step 4: .xyz only ----------
if (!selectedFile.toLowerCase().endsWith(".xyz")) {
	msg("Point Cloud Import",
		"Unsupported file type:\n\n" + selectedFile +
		"\n\nOnly .xyz files are supported right now.");
	print("PointCloud Import stopped: unsupported file type.");
	return 0;
}

// ---------- Step 5: file name and a Windows-style source path ----------
string sourcePath = selectedFile.split("/").join("\\");
int sepIdx = sourcePath.lastIndexOf("\\");
string fileName = sourcePath.slice(sepIdx + 1);

print("Selected file:   " + sourcePath);

// ---------- Step 5b: validate the file contents ----------
// Skips every header or comment line until the first DATA row: a line whose first
// three tokens look numeric when split by comma, tab or space (tried in that order).
// That first data row is the parsing map for the whole import: it sets the
// delimiter, the column count (RGB if 6+), and how many lines the mesh build skips.
// Then up to sampleLines data rows are checked. Nothing is created if this fails.
Array numStarts = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "-", "+", "."];
Array digits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];
Array delimCandidates = [",", "\t", " "];
string delim = "";        // set by the first data row
int headerLines = 0;      // physical lines before the first data row (skipped by the mesh build)
int numCols = 0;
int sampled = 0;          // data rows sampled, including the first one
int badSample = 0;
int nonDataLines = 0;     // non-empty lines skipped before the first data row
int physLine = 0;         // physical line number, counts blank lines too
string firstHeader = "";

fileopen(sourcePath, "r");
while (!endoffile() && sampled < sampleLines) {
	string sLine = filereadline().trim();
	physLine++;
	if (sLine.length == 0)
		continue;

	// Before the first data row try every delimiter; after it, only the chosen one
	Array tryDelims = delimCandidates;
	if (delim.length > 0)
		tryDelims = [delim];

	int isData = 0;
	int tokCount = 0;
	string foundDelim = "";
	for (int d = 1; d <= tryDelims.length && !isData; d++) {
		string dl = tryDelims[d];

		// Split and drop empty tokens (handles repeated spaces)
		Array rawS = sLine.split(dl);
		Array tokS = [];
		for (int k = 1; k <= rawS.length; k++) {
			string t = rawS[k].trim();
			if (t.length > 0)
				tokS.push(t);
		}
		if (tokS.length < 3)
			continue;

		// X, Y and Z must each start like a number AND contain a digit
		// (so "--", "+" or "." on their own are not numbers)
		int numericCount = 0;
		for (int q = 1; q <= 3; q++) {
			string tq = tokS[q];
			int startIdx = 0;
			for (int s = 1; s <= numStarts.length; s++) {
				string ns = numStarts[s];
				if (tq.startsWith(ns)) {
					startIdx = s;
					break;
				}
			}
			if (startIdx == 0)
				break;
			int hasDigit = (startIdx <= 10);   // starts with a digit
			if (!hasDigit) {
				for (int g = 1; g <= digits.length; g++) {
					string dg = digits[g];
					if (tq.includes(dg)) {
						hasDigit = 1;
						break;
					}
				}
			}
			if (!hasDigit)
				break;
			numericCount++;
		}

		if (numericCount == 3) {
			isData = 1;
			tokCount = tokS.length;
			foundDelim = dl;
		}
	}

	if (delim.length == 0) {
		// Still looking for the first data row
		if (!isData) {
			nonDataLines++;
			if (firstHeader.length == 0)
				firstHeader = sLine;
			print("Header/comment:  line " + string.fromNum(physLine) + ": " + sLine);
			if (nonDataLines >= maxHeaderLines)
				break;
			continue;
		}
		// First data row = the parsing map
		delim = foundDelim;
		numCols = tokCount;
		headerLines = physLine - 1;
	} else if (!isData) {
		badSample++;
	}
	sampled++;
}
fileclose();

if (numCols < 3) {
	string reason = "Could not find X Y Z data in:\n\n" + sourcePath;
	if (nonDataLines >= maxHeaderLines)
		reason += "\n\nThe first " + string.fromNum(maxHeaderLines) + " non-empty lines were all header or comment text.";
	msg("Point Cloud Import",
		reason + "\n\nExpected data lines like: 0.6983 3.2403 -1.2932 84 89 90");
	print("PointCloud Import stopped: no valid X Y Z lines found.");
	return 0;
}

int hasRGB = (numCols >= 6);
string delimName = "space";
if (delim == ",")
	delimName = "comma";
else if (delim == "\t")
	delimName = "tab";

print("Validation:      " + delimName + "-delimited, " + string.fromNum(numCols) + " columns" +
	", lines before data: " + string.fromNum(headerLines) +
	" (" + string.fromNum(nonDataLines) + " header/comment)" +
	", RGB: " + (hasRGB ? "yes" : "no (using gray)") +
	", bad sample lines: " + string.fromNum(badSample));

// ================= PART 3: reserve PointCloud_N, create its folder, copy the file =================

// ---------- Step 6: starting number = count of PointCloud_ objects + 1 ----------
treenode modelRoot = model();
string prefixLower = namePrefix.toLowerCase();

int count = 0;
for (int i = 1; i <= modelRoot.subnodes.length; i++) {
	if (modelRoot.subnodes[i].name.toLowerCase().startsWith(prefixLower))
		count++;
}

// ---------- Step 7: find the first N where neither the object nor the folder exists ----------
int n = count + 1;
string newName = "";
string subFolderPath = "";
string subMarker = "";

while (1) {
	newName = namePrefix + string.fromNum(n);
	subFolderPath = folderPath + "\\" + newName;
	subMarker = subFolderPath + "\\" + markerName;

	// Object check (case-insensitive)
	int objectTaken = 0;
	string newLower = newName.toLowerCase();
	for (int j = 1; j <= modelRoot.subnodes.length; j++) {
		if (modelRoot.subnodes[j].name.toLowerCase() == newLower) {
			objectTaken = 1;
			break;
		}
	}

	if (objectTaken) {
		print("Skipping " + newName + ": object already in model.");
	} else if (fileexists(subMarker)) {
		print("Skipping " + newName + ": folder already exists.");
	} else {
		break;   // this N is free
	}
	n++;
}

print("Using name:      " + newName);

// ---------- Step 8: create PointCloudFiles\PointCloud_N and its marker ----------
applicationcommand("createdirectory", subFolderPath);

int tries2 = 0;
while (!fileexists(subMarker) && tries2 < maxTries) {
	fileopen(subMarker, "w");
	fpt("Object: " + newName);
	fpr();
	fpt("Source: " + sourcePath);
	fpr();
	fpt("File:   " + fileName);
	fpr();
	fileclose();
	tries2++;
}

if (!fileexists(subMarker)) {
	msg("Point Cloud Import", "Could not create or write to folder:\n\n" + subFolderPath);
	print("PointCloud Import stopped: could not create " + subFolderPath);
	return 0;
}
print("Folder ready:    " + subFolderPath);

// ---------- Step 9: copy the .xyz into the new folder (optional) ----------
// runprogram() starts the copy and returns immediately (it does not wait),
// so the copy finishes in the background. This run reads from sourcePath.
string copyPath = subFolderPath + "\\" + fileName;
if (copySourceFile) {
	runprogram("cmd.exe /c copy /Y \"" + sourcePath + "\" \"" + copyPath + "\"");
	print("Copy started:    " + copyPath);
} else {
	print("Copy skipped:    copySourceFile = 0");
}

// ================= Create the PointCloud object =================
Object pc = Object.create("VisualTool");   // creates in the model by default
pc.name = newName;
pc.location = Vec3(0, 0, 0);
pc.size = Vec3(1, 1, 1);

// Labels (paths relative to the model folder so the model stays portable)
pc.labels.assert("PointCloudFolder").value = folderName + "/" + newName + "/";
if (copySourceFile)
	pc.labels.assert("PointCloudFile").value = folderName + "/" + newName + "/" + fileName;
else
	pc.labels.assert("PointCloudFile").value = sourcePath;   // not copied: points at the original
pc.labels.assert("PointCloudSource").value = sourcePath;

// Display controls - edit these labels any time, no re-import needed
pc.labels.assert("PointSize").value = pointSize;   // point size in pixels
pc.labels.assert("Visible").value = 1;             // 1 = show cloud, 0 = hide cloud
pc.labels.assert("ShowBox").value = 0;             // 1 = also draw the Visual Tool shape, 0 = hide it

print("Created object:  " + newName);

// ================= On Draw trigger =================
// Draws the point mesh stored in the object's "Mesh" label as GL_POINTS.
// Reads the PointSize and Visible labels every frame.
// Guard line: nothing is drawn (and no errors) until the Mesh label exists.
string drawCode =
	"/**Custom Code*/\n" +
	"treenode current = ownerobject(c);\n" +
	"treenode view = parnode(1);\n" +
	"\n" +
	"// If this function returns a true, the default draw code of the object will not be executed.\n" +
	"treenode meshNode = label(current, \"Mesh\");\n" +
	"if (!objectexists(meshNode))\n" +
	"\treturn 0;\n" +
	"\n" +
	"// Visible label: 0 hides the cloud\n" +
	"treenode visNode = label(current, \"Visible\");\n" +
	"if (objectexists(visNode) && getnodenum(visNode) == 0)\n" +
	"\treturn 0;\n" +
	"\n" +
	"// PointSize label: point size in pixels (default 4)\n" +
	"double ps = 4;\n" +
	"treenode psNode = label(current, \"PointSize\");\n" +
	"if (objectexists(psNode) && getnodenum(psNode) > 0)\n" +
	"\tps = getnodenum(psNode);\n" +
	"\n" +
	"// Draw in model units so the object's size does not stretch the cloud\n" +
	"drawtomodelscale(current);\n" +
	"fglRotate(-90, 1, 0, 0);\n" +
	"fglDisable(GL_TEXTURE_2D);\n" +
	"glPointSize(ps);\n" +
	"meshdraw(meshNode, GL_POINTS, 0, 0);\n" +
	"fglEnable(GL_TEXTURE_2D);\n" +
	"\n" +
	"// ShowBox label: 1 = also draw the Visual Tool's own shape, 0 = hide it\n" +
	"treenode sbNode = label(current, \"ShowBox\");\n" +
	"if (objectexists(sbNode) && getnodenum(sbNode) == 1)\n" +
	"\treturn 0;\n" +
	"return 1;   // skip the default draw (hides the Visual Tool shape)\n";

treenode onDrawNode = assertvariable(pc, "ondrawtrigger", DATATYPE_STRING);
onDrawNode.value = drawCode;
switch_flexscript(onDrawNode, 1);
buildnodeflexscript(onDrawNode);
rebindobjectattributes(pc);

print("On Draw trigger set on " + newName);

// ================= PART 4: build the point mesh =================
// One pass through the file: every valid line becomes one vertex (one point).
treenode meshNode = pc.labels.assert("Mesh");
mesh(meshNode, MESH_POSITION | MESH_AMBIENT_AND_DIFFUSE, 0);

int pointCount = 0;
int skipped = 0;
int lineNum = 0;
double minX = 0; double maxX = 0;
double minY = 0; double maxY = 0;
double minZ = 0; double maxZ = 0;

fileopen(sourcePath, "r");
while (!endoffile()) {
	string line = filereadline();
	lineNum++;
	if (lineNum <= headerLines)
		continue;

	line = line.trim();
	if (line.length == 0)
		continue;

	Array v = line.split(delim);

	// Fast path: exact column count. Otherwise drop empty tokens (repeated delimiters).
	if (v.length != numCols) {
		Array cleaned = [];
		for (int k = 1; k <= v.length; k++) {
			if (v[k].trim().length > 0)
				cleaned.push(v[k].trim());
		}
		v = cleaned;
	}

	if (v.length < 3) {
		skipped++;
		continue;
	}

	// Does X look like a number? (skips stray text and comment lines like "-- note")
	// Digits are first in numStarts, so normal rows match within the first few checks.
	string xTok = v[1];
	int xNumeric = 0;
	for (int s = 1; s <= numStarts.length; s++) {
		string ns = numStarts[s];
		if (xTok.startsWith(ns)) {
			if (s <= 10) {
				xNumeric = 1;                       // starts with a digit
			} else {
				for (int g = 1; g <= digits.length; g++) {
					string dg = digits[g];
					if (xTok.includes(dg)) {        // sign or "." must be followed by a digit somewhere
						xNumeric = 1;
						break;
					}
				}
			}
			break;
		}
	}
	if (!xNumeric) {
		skipped++;
		continue;
	}

	double px = v[1].toNum() * coordScale;
	double py = v[2].toNum() * coordScale;
	double pz = v[3].toNum() * coordScale;

	double r = defaultGray;
	double g = defaultGray;
	double b = defaultGray;
	if (hasRGB && v.length >= 6) {
		r = v[4].toNum() / colorScale;
		g = v[5].toNum() / colorScale;
		b = v[6].toNum() / colorScale;
	}

	int vi = meshaddvertex(meshNode);
	meshsetvertexattrib(meshNode, vi, MESH_POSITION, px, py, pz);
	meshsetvertexattrib(meshNode, vi, MESH_AMBIENT_AND_DIFFUSE, r, g, b);

	// Bounds
	if (pointCount == 0) {
		minX = px; maxX = px; minY = py; maxY = py; minZ = pz; maxZ = pz;
	} else {
		if (px < minX) minX = px;
		if (px > maxX) maxX = px;
		if (py < minY) minY = py;
		if (py > maxY) maxY = py;
		if (pz < minZ) minZ = pz;
		if (pz > maxZ) maxZ = pz;
	}

	pointCount++;
	if (pointCount % progressEvery == 0)
		print("  ... " + string.fromNum(pointCount) + " points loaded");
}
fileclose();

// Record results on the object
pc.labels.assert("PointCount").value = pointCount;
pc.labels.assert("SkippedLines").value = skipped;
pc.labels.assert("FileUnitToMeters").value = fileUnitToMeters;
pc.labels.assert("CoordScale").value = coordScale;

print("Mesh built:      " + string.fromNum(pointCount) + " points, " + string.fromNum(skipped) + " lines skipped");
print("Bounds X:        " + string.fromNum(minX) + " to " + string.fromNum(maxX));
print("Bounds Y:        " + string.fromNum(minY) + " to " + string.fromNum(maxY));
print("Bounds Z:        " + string.fromNum(minZ) + " to " + string.fromNum(maxZ));

repaintall();

// ================= Summary =================
if (showSummary) {
	string copyText = "not copied (original file used)";
	if (copySourceFile)
		copyText = folderName + "\\" + newName + "\\" + fileName;

	msg("Point Cloud Import - " + newName,
		"Object:     " + newName + "\n" +
		"Source:     " + fileName + "\n" +
		"Copy:       " + copyText + "\n\n" +
		"Points:     " + string.fromNum(pointCount) + "\n" +
		"Skipped:    " + string.fromNum(skipped) + " lines\n" +
		"Scale:      1 file unit = " + string.fromNum(coordScale, 4) + " model units\n\n" +
		"Bounds X:   " + string.fromNum(minX, 3) + "  to  " + string.fromNum(maxX, 3) + "\n" +
		"Bounds Y:   " + string.fromNum(minY, 3) + "  to  " + string.fromNum(maxY, 3) + "\n" +
		"Bounds Z:   " + string.fromNum(minZ, 3) + "  to  " + string.fromNum(maxZ, 3) + "\n\n" +
		"Edit the PointSize, Visible and ShowBox labels on " + newName + " to change the display.");
}

return pc;
