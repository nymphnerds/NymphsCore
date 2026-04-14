# Nymphs Marketing Positioning Draft

Date: `2026-04-09`
Status: draft for remake branch discussion

## Purpose

This document turns the current remake direction into a market-facing product story.

It exists to answer:

- what Nymphs is
- who it is for
- why someone would buy it instead of assembling their own tool stack
- how it should be positioned against general-purpose tools such as ComfyUI

## Working Product Definition

The current product direction is:

- `Nymphs2D` for image generation inside the Nymphs workflow
- `Hunyuan 2mv` for shape generation
- Blender-first finishing workflow
- manual user-supplied images stay fully supported
- generated outputs remain editable inside Blender

In plain terms:

Nymphs is becoming a Blender-first local AI asset creation workflow, not a general AI node editor.

## Core Product Promise

The product promise should be simple:

- generate images
- turn them into 3D
- finish them in Blender
- stay in one workflow

Stronger version:

Generate concept images, turn them into usable 3D, and keep full control inside Blender.

## What Nymphs Is Not

This matters for positioning.

Nymphs should not be presented as:

- a ComfyUI clone
- a generic diffusion graph editor
- a research sandbox for every possible backend
- a “supports everything” launcher with no clear workflow

That is not a sellable product story.

## What Nymphs Should Be

Nymphs should be presented as:

- a curated Blender workflow
- local-first
- image-to-3D focused
- practical rather than experimental-feeling
- centered on editable results, not black-box output

## Positioning Against ComfyUI

The correct message is not:

- “ComfyUI is bad”

The correct message is:

- ComfyUI is a powerful general-purpose node system
- Nymphs is a focused Blender-first workflow for people who want results, not graph building

ComfyUI strengths:

- huge flexibility
- deep graph control
- strong community workflows
- broad model experimentation

Nymphs strengths:

- runs in Blender
- oriented around 3D creation and finishing
- fewer moving parts for the user
- direct path from prompt or images to shape to finish
- less context switching
- easier to learn for artists who care about outputs more than graph authoring

Short positioning line:

Nymphs is not trying to replace ComfyUI for graph tinkerers. It is trying to replace workflow friction for Blender users.

## Ideal Customer Profiles

### 1. Blender Artists

People who:

- already work in Blender
- want AI assistance without leaving Blender
- care about editable materials and painting
- do not want to learn a separate node-graph ecosystem just to get started

### 2. Indie Game Developers

People who:

- need concept-to-asset speed
- need practical local workflows
- want to generate and refine game-ready ideas quickly
- value privacy and offline control

### 3. Modders

People who:

- need fast iteration
- want to derive assets from reference images
- care about material cleanup and paintover inside Blender

### 4. Hobbyists Who Find AI Toolchains Overwhelming

People who:

- are curious about AI-assisted 3D workflows
- are put off by ComfyUI complexity
- want a more guided path

## The Problem Nymphs Solves

Current local AI workflows often feel fragmented:

- one tool for prompt generation
- one tool for image generation
- another tool for image cleanup
- another tool for 3D generation
- another tool for material editing
- manual file hunting between all of them

Nymphs should solve that fragmentation.

The real problem statement:

People do not just want an image model or a 3D model. They want a coherent asset workflow.

## Why This Product Can Sell

The product can sell if it does these things well:

- reduces friction
- saves time
- keeps users in Blender
- produces editable results
- feels more guided than assembling raw open-source tools by hand

The commercial value is not:

- exclusive access to a model

The commercial value is:

- workflow design
- integration
- curation
- speed to usable result

## Strongest Differentiators

These are the strongest product differentiators right now:

- Blender-native workflow
- local/private backend control
- support for both manual images and generated images
- direct bridge from image generation into shape generation
- editable output instead of fully locked final assets
- future Blender-side retexture and finishing path

## Best Product Story

The strongest simple story is:

1. Start from a prompt or your own images.
2. Generate or prepare a usable image set.
3. Turn it into a 3D mesh.
4. Keep working on it directly in Blender.

That is much easier to understand than a backend-centric story.

## Messaging Themes

### Theme 1: Blender-First

Possible angle:

- AI asset creation without leaving Blender

### Theme 2: Curated, Not Chaotic

Possible angle:

- powerful local AI workflows without node-graph chaos

### Theme 3: Editable Results

Possible angle:

- generate fast, then actually finish it properly

### Theme 4: Local Control

Possible angle:

- your assets, your machine, your workflow

## Headline Ideas

These are draft ideas, not final copy.

- AI-Assisted 3D Creation, Built for Blender
- Generate Images, Turn Them Into 3D, Finish Without Leaving Blender
- A Blender-First Local AI Workflow for Real Asset Creation
- From Prompt or Image to Editable 3D, Inside Blender
- Faster Local AI Asset Creation Without the Workflow Mess

## Short Tagline Ideas

- Blender-first AI asset workflow
- Local AI creation for Blender artists
- Image to 3D, finished in Blender
- Generate fast, finish properly

## Landing Page Structure

### 1. Hero

Show:

- Blender UI
- generated image
- resulting 3D mesh
- editable material in Blender

Main message:

- prompt or image to editable 3D inside Blender

### 2. Problem / Pain

Explain:

- too many disconnected tools
- too much setup friction
- too much graph complexity for normal artists

### 3. Solution

Show the workflow:

1. generate or supply image
2. create shape
3. refine in Blender

### 4. Key Benefits

- Blender-native
- local-first
- editable results
- guided workflow

### 5. Example Outputs

Need examples for:

- stylized
- realistic
- single-image path
- multiview path later

### 6. Why Not Just ComfyUI

Keep this calm and factual:

- ComfyUI is powerful for graph builders
- Nymphs is for artists who want a cleaner Blender workflow

## Proof Points Needed Before Strong Marketing

These are the product proof points that marketing needs:

- image generation is reliable enough
- shape generation feels consistent enough
- outputs are easy to find and reuse
- Blender finishing genuinely feels integrated
- the workflow is faster than assembling the same stack manually

Without those, the marketing claim becomes weak.

## Risks To Be Honest About Internally

- `2mv` texturing is still slow
- image-model choice still needs benchmarking
- multiview generation is not solved yet
- some parts still feel experimental
- extension feed caching is occasionally annoying during testing

These are not front-page messages, but they matter for roadmap honesty.

## Marketing Priorities

If time is limited, the highest-value marketing work is:

1. clarify the core promise
2. capture a clean end-to-end demo
3. show Blender-native value
4. show editable output, not just generation
5. avoid overclaiming “one-click perfection”

## Immediate Marketing Tasks

### 1. Create A Product One-Liner

Need one sentence that survives everywhere:

- website
- GitHub
- extension feed
- screenshots
- video intro

### 2. Capture The Hero Workflow

Need a short demo showing:

- prompt or image input
- image generation
- shape generation
- Blender finishing

### 3. Collect Before/After Screens

Especially:

- source image
- generated mesh
- final Blender-finished result

### 4. Decide The Main Audience

The easiest first audience is probably:

- Blender artists and indie devs

not:

- broad “everyone interested in AI”

### 5. Decide The Default Story

Most likely best story:

- image to 3D in Blender

not:

- text to 3D benchmark tool

## Immediate Recommendation

The marketing direction should be:

- Blender-first
- workflow-first
- editable-results-first

Do not market Nymphs as a backend buffet.
Market it as the practical local Blender workflow for creating and finishing AI-assisted 3D assets.
