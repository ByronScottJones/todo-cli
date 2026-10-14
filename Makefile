# Todo CLI task runner. Run `make` or `make help` to list targets.
# Namespaced targets live in tasks/Makefile.<namespace> and use `/` as the delimiter.

-include tasks/Makefile.*

# dotnet executable; override to use a specific SDK (e.g. DOTNET=/opt/dotnet/dotnet).
DOTNET ?= dotnet
# Solution that every build, test, and format target operates on.
SOLUTION ?= src/Todo.CLI.sln
# Build configuration.
CONFIGURATION ?= Release
# Extra arguments passed to every dotnet build/test invocation (e.g. DOTNET_ARGS="-m:1 -nodeReuse:false").
DOTNET_ARGS ?=
# Runtime identifier used by `publish`; defaults to the host platform.
RID ?= $(shell $(DOTNET) --info | sed -n 's/^ *RID: *//p')
# Output directory for `publish`.
PUBLISH_DIR ?= src/publish
# Installation directory used by `install`.
PREFIX ?= $(HOME)/.local

.DEFAULT_GOAL := help

.PHONY: help default all deps build test check publish install uninstall clean

## This help screen
help:
	@printf "Available targets:\n\n"
	@awk '/^[a-zA-Z\-_0-9%:\\\/]+/ { \
		helpMessage = match(lastLine, /^## (.*)/); \
		if (helpMessage) { \
			helpCommand = $$1; \
			helpMessage = substr(lastLine, RSTART + 3, RLENGTH); \
			gsub("\\\\", "", helpCommand); \
			gsub(":+$$", "", helpCommand); \
			printf "  \x1b[32;01m%-35s\x1b[0m %s\n", helpCommand, helpMessage; \
		} \
	} \
	{ lastLine = $$0 }' $(MAKEFILE_LIST) | sort -u
	@printf "\n"

## Alias for help
default: help

## Run every quality gate: format check, markdown lint, and the full test suite
all: check

## Restore NuGet packages
deps:
	scripts/require-tool.sh $(DOTNET) "install the .NET SDK from https://dot.net"
	$(DOTNET) restore $(SOLUTION)

## Build the solution (warnings are errors)
build: deps
	$(DOTNET) build $(SOLUTION) -c $(CONFIGURATION) --no-restore $(DOTNET_ARGS)

## Run the unit tests
test: build
	$(DOTNET) test $(SOLUTION) -c $(CONFIGURATION) --no-build $(DOTNET_ARGS)

## Run the same gates as CI: lint/fmt, lint/md, test
check: lint/fmt lint/md test

## Publish a self-contained single-file executable for RID into PUBLISH_DIR
publish: deps
	$(DOTNET) publish src/Todo.CLI -c $(CONFIGURATION) -r $(RID) -o $(PUBLISH_DIR) $(DOTNET_ARGS)

## Install the published executable into PREFIX/bin
install: publish
	install -d $(PREFIX)/bin
	install -m 0755 $(PUBLISH_DIR)/todo $(PREFIX)/bin/todo

## Remove the installed executable
uninstall:
	rm -f $(PREFIX)/bin/todo

## Remove build and publish output
clean:
	$(DOTNET) clean $(SOLUTION) -c $(CONFIGURATION) $(DOTNET_ARGS)
	rm -rf $(PUBLISH_DIR)
